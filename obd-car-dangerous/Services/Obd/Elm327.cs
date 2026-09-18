using System.Text;

namespace obd_car_dangerous.Services.Obd
{
    /// <summary>
    /// Talks to an ELM327 (v1.3 - v2.x, including the common v1.5 clones) over any transport.
    /// All calls are synchronous and must be made from a single thread - <see cref="ObdLink"/> owns one.
    /// </summary>
    internal sealed class Elm327 : IDisposable
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(1200);
        private static readonly TimeSpan ResetTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

        private readonly IObdTransport transport;

        public Elm327(IObdTransport transport)
        {
            this.transport = transport;
        }

        public string Firmware { get; private set; } = "ELM327";

        public string Protocol { get; private set; } = "unknown";

        public bool IsOpen => transport.IsOpen;

        /// <summary>Last raw exchange, handy when a car refuses to answer.</summary>
        public string LastResponse { get; private set; } = string.Empty;

        /// <summary>
        /// Opens the link, configures the adapter and asks the ECU for supported PIDs.
        /// Returns false with a reason when this port is not an ELM327 or the car is not answering.
        /// </summary>
        /// <param name="handshakeTimeout">
        /// How long to wait for the adapter to identify itself. Keep it short while probing ports,
        /// longer once the right port is known - a cold clone can take a second to wake up.
        /// </param>
        public bool Initialize(out string error, TimeSpan? handshakeTimeout = null)
        {
            error = string.Empty;
            TimeSpan handshake = handshakeTimeout ?? ResetTimeout;

            try
            {
                transport.Open();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            string reset = Send("ATZ", handshake);
            if (!reset.Contains("ELM", StringComparison.OrdinalIgnoreCase))
            {
                // Some clones answer ATI when ATZ is swallowed during power up.
                reset = Send("ATI", handshake);
                if (!reset.Contains("ELM", StringComparison.OrdinalIgnoreCase))
                {
                    error = "No ELM327 answer";
                    return false;
                }
            }

            Firmware = reset.Split('\r', '\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.Contains("ELM", StringComparison.OrdinalIgnoreCase)) ?? "ELM327";

            Send("ATE0");   // echo off - keeps parsing simple
            Send("ATL0");   // no line feeds
            Send("ATS0");   // no spaces in responses
            Send("ATH0");   // no headers
            Send("ATAT1");  // adaptive timing
            Send("ATSP0");  // let the adapter work out the protocol

            // First real request also triggers the protocol search.
            string probe = Send("0100", ProbeTimeout);
            if (!Clean(probe).Contains("41"))
            {
                error = Describe(probe);
                return false;
            }

            Protocol = Send("ATDP").Split('\r', '\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "unknown";
            if (Protocol.StartsWith("AUTO,", StringComparison.OrdinalIgnoreCase))
            {
                Protocol = Protocol[5..].Trim();
            }

            return true;
        }

        /// <summary>Sends one command and returns the raw text the adapter replied with.</summary>
        public string Send(string command, TimeSpan? timeout = null)
        {
            transport.Write(command + "\r");
            string response = transport.ReadUntilPrompt(timeout ?? DefaultTimeout);
            LastResponse = response;
            return response;
        }

        /// <summary>Reads a mode 01 PID and returns its data bytes, or null when the ECU has no answer.</summary>
        public byte[]? ReadPid(byte pid)
        {
            string response = Send($"01{pid:X2}");
            return Payload(response, 0x41, pid);
        }

        /// <summary>Reads one PID out of freeze frame 0 (mode 02).</summary>
        public byte[]? ReadFreezeFrame(byte pid)
        {
            string response = Send($"02{pid:X2}00");
            return Payload(response, 0x42, pid, skipExtra: 1);
        }

        /// <summary>Bit masks of supported mode 01 PIDs, read in blocks of 32.</summary>
        public HashSet<byte> SupportedPids()
        {
            var supported = new HashSet<byte>();

            foreach (byte block in new byte[] { 0x00, 0x20, 0x40, 0x60 })
            {
                byte[]? data = ReadPid(block);
                if (data is null || data.Length < 4)
                {
                    break;
                }

                uint mask = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
                for (int bit = 0; bit < 32; bit++)
                {
                    if ((mask & (1u << (31 - bit))) != 0)
                    {
                        supported.Add((byte)(block + bit + 1));
                    }
                }

                // The top bit says whether the next block exists.
                if ((mask & 1) == 0)
                {
                    break;
                }
            }

            return supported;
        }

        /// <summary>Stored (03), pending (07) or permanent (0A) trouble codes.</summary>
        public List<string> ReadTroubleCodes(string mode)
        {
            byte header = mode switch
            {
                "07" => 0x47,
                "0A" => 0x4A,
                _ => 0x43,
            };

            string response = Send(mode, TimeSpan.FromSeconds(3));
            string hex = Collect(response);
            var codes = new List<string>();

            int start = hex.IndexOf(header.ToString("X2"), StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return codes;
            }

            string body = hex[(start + 2)..];

            // CAN answers carry a count byte first; older protocols pad to three codes per frame.
            if (body.Length % 4 == 2 && body.Length >= 2)
            {
                body = body[2..];
            }

            for (int i = 0; i + 4 <= body.Length; i += 4)
            {
                string pair = body.Substring(i, 4);
                if (pair == "0000")
                {
                    continue;
                }

                string? code = DecodeDtc(pair);
                if (code is not null && !codes.Contains(code))
                {
                    codes.Add(code);
                }
            }

            return codes;
        }

        /// <summary>Mode 04 - clears codes and turns the check engine light off.</summary>
        public bool ClearTroubleCodes()
        {
            string response = Send("04", TimeSpan.FromSeconds(4));
            return Clean(response).Contains("44");
        }

        /// <summary>Mode 09 PID 02 - the VIN, or null when the ECU does not report one.</summary>
        public string? ReadVin()
        {
            string hex = Collect(Send("0902", TimeSpan.FromSeconds(3)));
            int start = hex.IndexOf("4902", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            string body = hex[(start + 4)..];
            if (body.StartsWith("01", StringComparison.OrdinalIgnoreCase))
            {
                body = body[2..];
            }

            var text = new StringBuilder();
            for (int i = 0; i + 2 <= body.Length; i += 2)
            {
                if (byte.TryParse(body.AsSpan(i, 2), System.Globalization.NumberStyles.HexNumber, null, out byte value)
                    && value >= 0x20 && value < 0x7F)
                {
                    text.Append((char)value);
                }
            }

            string vin = text.ToString().Trim();
            return vin.Length >= 11 ? vin[^Math.Min(17, vin.Length)..] : null;
        }

        /// <summary>Mode 09 PID 04 - calibration id string.</summary>
        public string? ReadCalibrationId()
        {
            string hex = Collect(Send("0904", TimeSpan.FromSeconds(3)));
            int start = hex.IndexOf("4904", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            string body = hex[(start + 4)..];
            var text = new StringBuilder();
            for (int i = 0; i + 2 <= body.Length; i += 2)
            {
                if (byte.TryParse(body.AsSpan(i, 2), System.Globalization.NumberStyles.HexNumber, null, out byte value)
                    && value >= 0x20 && value < 0x7F)
                {
                    text.Append((char)value);
                }
            }

            string id = text.ToString().Trim();
            return id.Length > 2 ? id : null;
        }

        /// <summary>Battery voltage measured by the adapter itself (ATRV).</summary>
        public float? ReadBatteryVoltage()
        {
            string response = Send("ATRV").Replace("V", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            string digits = new(response.Where(c => char.IsDigit(c) || c == '.').ToArray());
            return float.TryParse(digits, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float volts) && volts is > 5 and < 20
                ? volts
                : null;
        }

        // ---- response parsing ------------------------------------------------

        /// <summary>Data bytes of a positive answer, or null for NO DATA / errors.</summary>
        private static byte[]? Payload(string response, byte responseMode, byte pid, int skipExtra = 0)
        {
            string hex = Collect(response);
            string marker = $"{responseMode:X2}{pid:X2}";
            int start = hex.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            string body = hex[(start + marker.Length + skipExtra * 2)..];
            int count = body.Length / 2;
            if (count == 0)
            {
                return null;
            }

            var bytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                bytes[i] = byte.Parse(body.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            }

            return bytes;
        }

        /// <summary>
        /// Joins the hex of every data line, dropping chatter ("SEARCHING...", "NO DATA"),
        /// ISO-TP frame indices ("0:") and the multi frame length header.
        /// </summary>
        internal static string Collect(string response)
        {
            var hex = new StringBuilder();

            foreach (string rawLine in response.Split('\r', '\n'))
            {
                string line = rawLine.Replace(" ", string.Empty).Replace("\t", string.Empty).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon is >= 0 and <= 2)
                {
                    line = line[(colon + 1)..];
                }
                else if (line.Length == 3 && IsHex(line))
                {
                    // Length header of a multi frame answer.
                    continue;
                }

                if (line.Length < 2 || !IsHex(line))
                {
                    continue;
                }

                hex.Append(line.ToUpperInvariant());
            }

            return hex.ToString();
        }

        private static string Clean(string response) => Collect(response);

        private static bool IsHex(string text) => text.All(Uri.IsHexDigit);

        /// <summary>"0133" -> "P0133", per SAE J2012.</summary>
        internal static string? DecodeDtc(string pair)
        {
            if (pair.Length != 4 || !IsHex(pair))
            {
                return null;
            }

            int value = Convert.ToInt32(pair, 16);
            char letter = ((value >> 14) & 0x03) switch
            {
                0 => 'P',
                1 => 'C',
                2 => 'B',
                _ => 'U',
            };

            int first = (value >> 12) & 0x03;
            return $"{letter}{first}{(value & 0x0FFF):X3}";
        }

        /// <summary>Turns an unhelpful adapter reply into something worth showing a driver.</summary>
        internal static string Describe(string response)
        {
            string text = response.ToUpperInvariant();

            if (text.Contains("UNABLE TO CONNECT"))
            {
                return "Adapter could not reach the ECU - is the ignition on?";
            }

            if (text.Contains("NO DATA"))
            {
                return "ECU did not answer (NO DATA)";
            }

            if (text.Contains("CAN ERROR") || text.Contains("BUS ERROR") || text.Contains("BUS INIT"))
            {
                return "Bus error while talking to the ECU";
            }

            if (text.Contains("STOPPED"))
            {
                return "Request stopped by the adapter";
            }

            if (text.Contains("?"))
            {
                return "Adapter did not understand the command";
            }

            return "No usable answer from the vehicle";
        }

        public void Dispose() => transport.Dispose();
    }
}

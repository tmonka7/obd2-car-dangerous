using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

namespace obd_car_dangerous.Services.Obd
{
    /// <summary>A byte pipe to an ELM327: Bluetooth/USB serial port or a Wi-Fi socket.</summary>
    internal interface IObdTransport : IDisposable
    {
        string Name { get; }

        bool IsOpen { get; }

        void Open();

        void Close();

        void Write(string text);

        /// <summary>Reads until the ELM327 prompt '&gt;' or the timeout expires.</summary>
        string ReadUntilPrompt(TimeSpan timeout);

        /// <summary>Throws away anything the adapter has already queued.</summary>
        void DiscardBuffers();
    }

    /// <summary>ELM327 over a COM port - USB cables and Bluetooth SPP pairings both look like this.</summary>
    internal sealed class SerialObdTransport : IObdTransport
    {
        /// <summary>Baud rates worth trying on a clone adapter, most likely first.</summary>
        public static readonly int[] CommonBaudRates = { 38400, 115200, 9600, 500000, 57600, 230400 };

        private readonly SerialPort port;

        public SerialObdTransport(string portName, int baudRate)
        {
            BaudRate = baudRate;
            port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 400,
                WriteTimeout = 2000,
                DtrEnable = true,
                RtsEnable = true,
                NewLine = "\r",
                Handshake = Handshake.None,
                Encoding = Encoding.ASCII,
            };
        }

        public string Name => $"{port.PortName} @ {BaudRate}";

        public int BaudRate { get; }

        public bool IsOpen => port.IsOpen;

        public void Open()
        {
            if (!port.IsOpen)
            {
                port.Open();
            }

            DiscardBuffers();
        }

        public void Close()
        {
            if (port.IsOpen)
            {
                port.Close();
            }
        }

        public void Write(string text)
        {
            port.Write(text);
        }

        public string ReadUntilPrompt(TimeSpan timeout)
        {
            var builder = new StringBuilder();
            DateTime deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    int value = port.ReadByte();
                    if (value < 0)
                    {
                        continue;
                    }

                    char c = (char)value;
                    if (c == '>')
                    {
                        return builder.ToString();
                    }

                    builder.Append(c);
                }
                catch (TimeoutException)
                {
                    // Keep waiting until the caller's deadline; a slow ECU answer is normal.
                }
            }

            return builder.ToString();
        }

        public void DiscardBuffers()
        {
            if (!port.IsOpen)
            {
                return;
            }

            try
            {
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
            }
            catch (Exception)
            {
                // Some virtual Bluetooth ports refuse this; nothing to do about it.
            }
        }

        public void Dispose()
        {
            try
            {
                Close();
            }
            catch (Exception)
            {
                // Closing a yanked USB adapter can throw; ignore.
            }

            port.Dispose();
        }

        /// <summary>COM ports currently present on the machine.</summary>
        public static string[] PortNames()
        {
            try
            {
                string[] names = SerialPort.GetPortNames();
                Array.Sort(names, (a, b) => Number(a).CompareTo(Number(b)));
                return names;
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }

            static int Number(string name)
            {
                string digits = new(name.Where(char.IsDigit).ToArray());
                return int.TryParse(digits, out int value) ? value : 0;
            }
        }
    }

    /// <summary>ELM327 over Wi-Fi, which is a plain TCP socket (usually 192.168.0.10:35000).</summary>
    internal sealed class TcpObdTransport : IObdTransport
    {
        private readonly string host;
        private readonly int tcpPort;
        private TcpClient? client;
        private NetworkStream? stream;

        public TcpObdTransport(string host, int port)
        {
            this.host = host;
            tcpPort = port;
        }

        public string Name => $"{host}:{tcpPort}";

        public bool IsOpen => client?.Connected == true;

        public void Open()
        {
            client = new TcpClient();
            if (!client.ConnectAsync(host, tcpPort).Wait(TimeSpan.FromSeconds(4)))
            {
                client.Dispose();
                client = null;
                throw new IOException($"No answer from {host}:{tcpPort}");
            }

            client.NoDelay = true;
            stream = client.GetStream();
            stream.ReadTimeout = 400;
        }

        public void Close()
        {
            stream?.Dispose();
            client?.Close();
            stream = null;
            client = null;
        }

        public void Write(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            stream?.Write(bytes, 0, bytes.Length);
        }

        public string ReadUntilPrompt(TimeSpan timeout)
        {
            if (stream is null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            DateTime deadline = DateTime.UtcNow + timeout;
            var buffer = new byte[256];

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        continue;
                    }

                    string chunk = Encoding.ASCII.GetString(buffer, 0, read);
                    int prompt = chunk.IndexOf('>');
                    if (prompt >= 0)
                    {
                        builder.Append(chunk[..prompt]);
                        return builder.ToString();
                    }

                    builder.Append(chunk);
                }
                catch (IOException)
                {
                    // Read timeout inside the socket; keep waiting for the caller's deadline.
                }
            }

            return builder.ToString();
        }

        public void DiscardBuffers()
        {
            if (stream is null)
            {
                return;
            }

            var buffer = new byte[512];
            try
            {
                while (client!.Available > 0)
                {
                    stream.Read(buffer, 0, buffer.Length);
                }
            }
            catch (Exception)
            {
                // Nothing buffered, or the socket went away - both fine here.
            }
        }

        public void Dispose() => Close();
    }
}

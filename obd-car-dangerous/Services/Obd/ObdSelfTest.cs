using System.Text;

namespace obd_car_dangerous.Services.Obd
{
    /// <summary>
    /// Checks the ELM327 parsing against canned adapter answers - run with "--selftest".
    /// It needs no hardware, so it is the quickest way to prove a change to the protocol code.
    /// </summary>
    internal static class ObdSelfTest
    {
        public static int Run(TextWriter output)
        {
            int failures = 0;

            void Check(string name, object? actual, object? expected)
            {
                bool ok = Equals(actual?.ToString(), expected?.ToString());
                output.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}   got <{actual}> expected <{expected}>");
                if (!ok)
                {
                    failures++;
                }
            }

            // ---- line collection ------------------------------------------
            Check("spaces stripped", Elm327.Collect("41 0C 1A F8\r\r"), "410C1AF8");
            Check("search line dropped", Elm327.Collect("SEARCHING...\r410C1AF8\r"), "410C1AF8");
            Check("no data dropped", Elm327.Collect("NO DATA\r"), string.Empty);
            Check("multi frame joined",
                Elm327.Collect("014\r0:49020131 4434\r1:4750303052 35\r"),
                "490201314434475030305235");

            // ---- code decoding --------------------------------------------
            Check("P code", Elm327.DecodeDtc("0133"), "P0133");
            Check("C code", Elm327.DecodeDtc("4133"), "C0133");
            Check("B code", Elm327.DecodeDtc("8133"), "B0133");
            Check("U code", Elm327.DecodeDtc("C100"), "U0100");
            Check("catalyst code", Elm327.DecodeDtc("0420"), "P0420");

            // ---- PID maths -------------------------------------------------
            Check("rpm", Decode("rpm", 0x1A, 0xF8), 1726f);
            Check("speed", Decode("speed", 0x4B), 75f);
            Check("coolant", Decode("coolant", 0x5A), 50f);
            Check("maf", Decode("maf", 0x01, 0xF4), 5f);
            Check("throttle", Decode("throttle", 0xFF), 100f);
            Check("fuel level", Decode("fuellevel", 0x80)?.ToString("0.0"), "50.2");
            Check("timing advance", Decode("timing", 0x80), 0f);
            Check("short fuel trim", Decode("stft", 0x80), 0f);
            Check("battery", Decode("battery", 0x31, 0x38)?.ToString("0.00"), "12.60");

            // ---- adapter identification ------------------------------------
            Check("mini ELM327 clone",
                AdapterCatalog.Identify("OBDII", EndpointKind.BluetoothSpp).Family, AdapterFamily.Elm327Spp);
            Check("HH OBD Advanced BLE",
                AdapterCatalog.Identify("OBDBLE", EndpointKind.Ble).Family, AdapterFamily.Elm327Ble);
            Check("HH OBD over serial",
                AdapterCatalog.Identify("HHOBD", EndpointKind.BluetoothSpp).Model, "HH OBD Advanced");
            Check("Autel is proprietary",
                AdapterCatalog.Identify("Maxi-VCI Mini", EndpointKind.Ble).SpeaksElm327, false);
            Check("USB cable is ELM327",
                AdapterCatalog.Identify("COM5", EndpointKind.Serial).Family, AdapterFamily.Elm327Usb);
            Check("headphones are not adapters", AdapterCatalog.LooksLikeAdapter("WH-1000XM4"), false);
            Check("dongle names are adapters", AdapterCatalog.LooksLikeAdapter("V-LINK"), true);

            // ---- full exchanges against a scripted adapter -----------------
            var scripted = new ScriptedTransport(new Dictionary<string, string>
            {
                ["ATZ"] = "\rELM327 v1.5\r",
                ["ATE0"] = "OK\r",
                ["ATL0"] = "OK\r",
                ["ATS0"] = "OK\r",
                ["ATH0"] = "OK\r",
                ["ATAT1"] = "OK\r",
                ["ATSP0"] = "OK\r",
                ["ATDP"] = "AUTO, ISO 15765-4 (CAN 11/500)\r",
                ["0100"] = "41 00 BE 3F A8 13\r",
                ["010C"] = "41 0C 1A F8\r",
                ["03"] = "43 02 01 0B 01 41\r",
                ["07"] = "47 01 01 71\r",
                ["0A"] = "4A 00\r",
                ["04"] = "44\r",
                ["0902"] = "014\r0: 49 02 01 31 44 34\r1: 47 50 30 30 52 35\r2: 35 42 31 32 33 34\r3: 35 36\r",
                ["ATRV"] = "12.6V\r",
            });

            var elm = new Elm327(scripted);
            Check("handshake", elm.Initialize(out string error), true);
            Check("handshake error", error, string.Empty);
            Check("firmware", elm.Firmware, "ELM327 v1.5");
            Check("protocol", elm.Protocol, "ISO 15765-4 (CAN 11/500)");

            byte[]? rpm = elm.ReadPid(0x0C);
            Check("pid payload", rpm is null ? "null" : string.Join(',', rpm), "26,248");

            Check("stored codes", string.Join(',', elm.ReadTroubleCodes("03")), "P010B,P0141");
            Check("pending codes", string.Join(',', elm.ReadTroubleCodes("07")), "P0171");
            Check("permanent codes", string.Join(',', elm.ReadTroubleCodes("0A")), string.Empty);
            Check("clear accepted", elm.ClearTroubleCodes(), true);
            Check("vin", elm.ReadVin(), "1D4GP00R55B123456");
            Check("adapter voltage", elm.ReadBatteryVoltage(), 12.6f);

            output.WriteLine(failures == 0 ? "All ELM327 parser checks passed." : $"{failures} check(s) FAILED.");
            return failures;
        }

        private static float? Decode(string key, params byte[] data) =>
            ObdPids.All.First(p => p.Key == key).Decode(data);

        /// <summary>Replays canned answers so the protocol code can be tested without a car.</summary>
        private sealed class ScriptedTransport : IObdTransport
        {
            private readonly Dictionary<string, string> answers;
            private readonly StringBuilder pending = new();

            public ScriptedTransport(Dictionary<string, string> answers)
            {
                this.answers = answers;
            }

            public string Name => "scripted";

            public bool IsOpen { get; private set; }

            public void Open() => IsOpen = true;

            public void Close() => IsOpen = false;

            public void Write(string text)
            {
                string command = text.Trim().ToUpperInvariant();
                pending.Append(answers.TryGetValue(command, out string? answer) ? answer : "?\r");
            }

            public string ReadUntilPrompt(TimeSpan timeout)
            {
                string response = pending.ToString();
                pending.Clear();
                return response;
            }

            public void DiscardBuffers() => pending.Clear();

            public void Dispose() => Close();
        }
    }
}

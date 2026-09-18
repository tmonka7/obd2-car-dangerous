using System.IO.Ports;
using System.Text;

namespace obd_car_dangerous.Services.Obd
{
    /// <summary>A byte pipe to an ELM327: a COM port, or a Bluetooth LE GATT link.</summary>
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
}

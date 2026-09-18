namespace obd_car_dangerous.Services
{
    internal enum LinkState
    {
        Disconnected,
        Connecting,
        Connected,
    }

    internal sealed record Adapter(string Name, string Transport, string Address);

    /// <summary>
    /// Stands in for the ELM327 link. Connect/Scan run on a timer so the UI shows the same
    /// progression it would with a real adapter.
    /// </summary>
    internal sealed class ConnectionService : IDisposable
    {
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 350 };
        private int ticks;
        private Action? pending;

        public ConnectionService()
        {
            timer.Tick += (_, _) =>
            {
                if (--ticks > 0)
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                    return;
                }

                timer.Stop();
                Action? action = pending;
                pending = null;
                action?.Invoke();
                Changed?.Invoke(this, EventArgs.Empty);
            };
        }

        public event EventHandler? Changed;

        public LinkState State { get; private set; } = LinkState.Connected;

        public bool IsConnected => State == LinkState.Connected;

        public bool IsBusy => timer.Enabled;

        public Adapter Current { get; private set; } = new("ELM327", "Bluetooth", "00:1D:A5:68:98:8B");

        public string Protocol { get; private set; } = "ISO 15765-4 (CAN)";

        public string Firmware => "ELM327 v2.1";

        public int SignalStrength { get; private set; } = 88;

        public List<Adapter> Found { get; } = new()
        {
            new Adapter("ELM327", "Bluetooth", "00:1D:A5:68:98:8B"),
            new Adapter("OBDII Wi-Fi", "Wi-Fi", "192.168.0.10:35000"),
            new Adapter("Vgate iCar Pro", "Bluetooth LE", "D8:80:39:FE:12:04"),
        };

        public string StatusText => State switch
        {
            LinkState.Connected => "Connected",
            LinkState.Connecting => "Connecting" + new string('.', 3 - Math.Max(0, ticks % 3)),
            _ => "Disconnected",
        };

        public void Connect(Adapter? adapter = null)
        {
            if (State == LinkState.Connecting)
            {
                return;
            }

            if (adapter is not null)
            {
                Current = adapter;
            }

            State = LinkState.Connecting;
            Start(6, () =>
            {
                State = LinkState.Connected;
                Protocol = Current.Transport == "Wi-Fi" ? "ISO 15765-4 (CAN 11/500)" : "ISO 15765-4 (CAN)";
                SignalStrength = Random.Shared.Next(72, 99);
                AppState.Dtc.Log("LINK", $"Connected to {Current.Name} ({Current.Transport})", AlarmLevel.Info);
            });
        }

        public void Disconnect()
        {
            timer.Stop();
            pending = null;
            State = LinkState.Disconnected;
            AppState.Dtc.Log("LINK", "Adapter disconnected", AlarmLevel.Warning);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Scan()
        {
            if (IsBusy)
            {
                return;
            }

            Scanning = true;
            Start(5, () =>
            {
                Scanning = false;
                SignalStrength = Random.Shared.Next(72, 99);
            });
        }

        public bool Scanning { get; private set; }

        private void Start(int steps, Action onDone)
        {
            ticks = steps;
            pending = onDone;
            timer.Start();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => timer.Dispose();
    }

    /// <summary>Static vehicle identity read from the ECU.</summary>
    internal static class Vehicle
    {
        public const string Make = "Toyota";
        public const string Model = "Corolla";
        public const string Year = "2020";
        public const string Vin = "JTDBR32ESLJ012345";
        public const string Engine = "1.8 L 2ZR-FAE";
        public const string EcuVersion = "v1.2.3";
        public const string CalibrationId = "89663-02T30";
    }
}

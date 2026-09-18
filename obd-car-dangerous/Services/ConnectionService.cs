using obd_car_dangerous.Services.Obd;

namespace obd_car_dangerous.Services
{
    internal enum LinkState
    {
        Disconnected,
        Connecting,
        Connected,
    }

    /// <summary>
    /// The app's view of the adapter link. Wraps <see cref="ObdLink"/> (a real ELM327 on a COM port
    /// or Wi-Fi socket) and falls back to a simulated feed when no adapter is present, so the UI is
    /// always usable. A UI timer re-publishes worker thread changes on the UI thread.
    /// </summary>
    internal sealed class ConnectionService : IDisposable
    {
        public static readonly ObdEndpoint DemoEndpoint = new("Demo (simulated)", EndpointKind.Demo, "demo");

        private readonly System.Windows.Forms.Timer pump = new() { Interval = 250 };
        private (bool Connected, bool Busy, string Status, int Rate) lastSeen;
        private bool demo;

        public ConnectionService()
        {
            Found.AddRange(ObdLink.Discover());
            Current = DemoEndpoint;

            pump.Tick += (_, _) =>
            {
                var now = (Link.IsConnected, Link.IsBusy, Link.Status, Link.ResponseRate);
                if (now != lastSeen)
                {
                    lastSeen = now;
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            };
            pump.Start();
        }

        public event EventHandler? Changed;

        public ObdLink Link { get; } = new();

        public List<ObdEndpoint> Found { get; } = new();

        public ObdEndpoint Current { get; private set; }

        /// <summary>True when a real adapter is streaming.</summary>
        public bool IsLive => Link.IsConnected;

        /// <summary>True when the screens are showing simulated values instead of a car.</summary>
        public bool IsDemo => demo && !Link.IsConnected;

        /// <summary>True when the UI should behave as connected, live or demo.</summary>
        public bool IsConnected => Link.IsConnected || IsDemo;

        public bool IsBusy => Link.IsBusy;

        public bool Scanning { get; private set; }

        public LinkState State => Link.IsConnected || IsDemo
            ? LinkState.Connected
            : Link.IsBusy ? LinkState.Connecting : LinkState.Disconnected;

        public string Protocol => Link.IsConnected
            ? Link.Protocol
            : IsDemo ? "ISO 15765-4 (CAN) · demo" : "-";

        public string Firmware => Link.IsConnected ? Link.Firmware : IsDemo ? "Simulator" : "-";

        public int SignalStrength => Link.IsConnected ? Link.ResponseRate : IsDemo ? 100 : 0;

        /// <summary>Whatever the link last reported - a failure reason, or the current step.</summary>
        public string Detail => Link.Status;

        public string StatusText => State switch
        {
            LinkState.Connected => IsDemo ? Loc.T("state.demo") : Loc.T("state.connected"),
            LinkState.Connecting => Loc.T("state.connecting") + "...",
            _ => Loc.T("state.disconnected"),
        };

        /// <summary>Connects to a real adapter, or switches to the simulated feed for the demo endpoint.</summary>
        public async Task<bool> ConnectAsync(ObdEndpoint endpoint, IProgress<string>? progress = null, bool quickProbe = false)
        {
            if (endpoint.Kind == EndpointKind.Demo)
            {
                Link.Disconnect();
                demo = true;
                Current = endpoint;
                AppState.Dtc.EnterDemoMode();
                Changed?.Invoke(this, EventArgs.Empty);
                AppState.Dtc.Log("LINK", "Demo mode - values are simulated", AlarmLevel.Info);
                return true;
            }

            demo = false;
            Current = endpoint;
            Changed?.Invoke(this, EventArgs.Empty);

            bool ok = await Link.ConnectAsync(endpoint, progress, quickProbe).ConfigureAwait(true);
            if (ok)
            {
                Current = Link.Endpoint ?? endpoint;
                AppState.Dtc.EnterLiveMode();
                AppState.Dtc.Log("LINK", $"Connected to {Link.Firmware} on {Current.Address}", AlarmLevel.Info);
                await AppState.Dtc.RefreshFromVehicleAsync().ConfigureAwait(true);
                AppState.MarkScanned();
            }
            else
            {
                AppState.Dtc.Log("LINK", $"Connection failed: {Link.Status}", AlarmLevel.Warning);
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return ok;
        }

        /// <summary>Fire and forget connect, used by the buttons.</summary>
        public void Connect(ObdEndpoint? endpoint = null) => _ = ConnectAsync(endpoint ?? Current);

        /// <summary>Switches to the simulated feed, for when no adapter is present.</summary>
        public void EnterDemo()
        {
            demo = true;
            Current = DemoEndpoint;
            AppState.Dtc.EnterDemoMode();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Tries every adapter we can see until one answers. Returns the endpoint that worked,
        /// or null when nothing did.
        /// </summary>
        public async Task<ObdEndpoint?> AutoConnectAsync(IProgress<string>? progress = null)
        {
            RefreshEndpoints();

            foreach (ObdEndpoint endpoint in Found.Where(e => e.Kind == EndpointKind.Serial))
            {
                if (await ConnectAsync(endpoint, progress).ConfigureAwait(true))
                {
                    return endpoint;
                }
            }

            return null;
        }

        public void Disconnect()
        {
            demo = false;
            Link.Disconnect();
            AppState.Dtc.Log("LINK", "Adapter disconnected", AlarmLevel.Warning);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Re-reads the list of COM ports.</summary>
        public void RefreshEndpoints()
        {
            List<ObdEndpoint> discovered = ObdLink.Discover();
            Found.Clear();
            Found.AddRange(discovered);
        }

        public async void Scan()
        {
            if (Scanning)
            {
                return;
            }

            Scanning = true;
            Changed?.Invoke(this, EventArgs.Empty);

            await Task.Run(RefreshEndpoints).ConfigureAwait(true);

            Scanning = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            pump.Dispose();
            Link.Dispose();
        }
    }

    /// <summary>Vehicle identity: read from the ECU when connected, demo values otherwise.</summary>
    internal static class Vehicle
    {
        private const string DemoVin = "JTDBR32ESLJ012345";

        public static string Vin => AppState.Connection.Link.Vin ?? (AppState.Connection.IsDemo ? DemoVin : "-");

        public static string CalibrationId =>
            AppState.Connection.Link.CalibrationId ?? (AppState.Connection.IsDemo ? "89663-02T30" : "-");

        /// <summary>World manufacturer identifier, decoded from the VIN prefix.</summary>
        public static string Make => Vin.Length >= 3 ? MakeFromWmi(Vin[..3]) : "-";

        public static string Model => AppState.Connection.IsDemo ? "Corolla" : "-";

        public static string Year
        {
            get
            {
                if (AppState.Connection.IsDemo)
                {
                    return "2020";
                }

                // Position 10 of a VIN encodes the model year.
                if (Vin.Length < 10)
                {
                    return "-";
                }

                const string codes = "ABCDEFGHJKLMNPRSTVWXY123456789";
                int index = codes.IndexOf(char.ToUpperInvariant(Vin[9]));
                return index < 0 ? "-" : (2010 + index).ToString();
            }
        }

        public static string Engine => AppState.Connection.IsDemo ? "1.8 L 2ZR-FAE" : "-";

        public static string EcuVersion => AppState.Connection.Link.CalibrationId is { Length: > 0 } id
            ? id
            : AppState.Connection.IsDemo ? "v1.2.3" : "-";

        private static string MakeFromWmi(string wmi) => wmi.ToUpperInvariant() switch
        {
            "JTD" or "JTE" or "JTH" or "JTL" or "JTM" or "JTN" or "SB1" or "VNK" => "Toyota",
            "JHM" or "JHL" or "SHH" or "1HG" or "2HG" or "19X" => "Honda",
            "JN1" or "JN8" or "SJN" or "1N4" => "Nissan",
            "JM1" or "JM3" or "4F2" => "Mazda",
            "WVW" or "WV1" or "WV2" or "3VW" or "1VW" => "Volkswagen",
            "WBA" or "WBS" or "4US" or "5UX" => "BMW",
            "WDB" or "WDD" or "WDC" or "4JG" => "Mercedes-Benz",
            "WAU" or "TRU" or "WA1" => "Audi",
            "KMH" or "KMF" or "TMA" => "Hyundai",
            "KNA" or "KNB" or "KND" or "U5Y" => "Kia",
            "1FA" or "1FT" or "1FM" or "WF0" => "Ford",
            "1G1" or "1GC" or "KL1" or "W0L" => "Chevrolet / Opel",
            "LSV" or "LFV" or "LVS" => "China market",
            _ => wmi,
        };
    }
}

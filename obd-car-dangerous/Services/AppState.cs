namespace obd_car_dangerous.Services
{
    internal sealed record SystemHealth(string Name, string Icon, string Status, int Score, string Detail);

    /// <summary>Single place every screen reads its data from.</summary>
    internal static class AppState
    {
        public static AppSettings Settings { get; } = AppSettings.Load();

        public static ConnectionService Connection { get; } = new();

        public static DtcStore Dtc { get; } = new();

        public static Telemetry Telemetry { get; } = new();

        public static DateTime LastScan { get; private set; } = DateTime.Now;

        public static void MarkScanned() => LastScan = DateTime.Now;

        /// <summary>Overall score out of 100, derived from live faults and sensor readings.</summary>
        public static int HealthScore
        {
            get
            {
                int score = 100;
                foreach (DtcRecord code in Dtc.All.Where(c => c.Status != DtcStatus.History))
                {
                    score -= code.Severity switch { "High" => 9, "Medium" => 5, _ => 2 };
                }

                if (Telemetry.BatteryVoltage < 12.2f)
                {
                    score -= 5;
                }

                if (Telemetry.CoolantTemp > Settings.CoolantLimit)
                {
                    score -= 10;
                }

                if (!Connection.IsConnected)
                {
                    score -= 4;
                }

                return Math.Clamp(score, 0, 100);
            }
        }

        public static string HealthLabel => HealthScore switch
        {
            >= 90 => "Excellent",
            >= 70 => "Good",
            >= 50 => "Fair",
            _ => "Poor",
        };

        /// <summary>Per-system rows for the diagnostics screen.</summary>
        public static IReadOnlyList<SystemHealth> Systems
        {
            get
            {
                return new[]
                {
                    Build("Engine", "engine", "Engine"),
                    Build("Transmission", "transmission", "Transmission"),
                    Build("ABS", "brake", "ABS"),
                    Build("Airbag", "airbag", "Airbag"),
                    Battery(),
                };

                static SystemHealth Build(string name, string icon, string system)
                {
                    DtcRecord[] faults = Dtc.All
                        .Where(c => c.Status != DtcStatus.History && c.System == system)
                        .ToArray();

                    if (faults.Length == 0)
                    {
                        return new SystemHealth(name, icon, "Good", 100, "No fault detected");
                    }

                    bool high = faults.Any(f => f.Severity == "High");
                    int score = Math.Max(30, 100 - faults.Length * (high ? 22 : 12));
                    string status = high ? "Fault" : "Warning";
                    return new SystemHealth(name, icon, status, score, $"{faults.Length} active code(s): {string.Join(", ", faults.Select(f => f.Code))}");
                }

                static SystemHealth Battery()
                {
                    float volts = Telemetry.BatteryVoltage;
                    bool weak = volts < 12.4f && Telemetry.Rpm < 900;
                    return new SystemHealth(
                        "Battery",
                        "battery",
                        weak ? "Warning" : "Good",
                        weak ? 62 : 94,
                        $"Resting voltage {volts:0.0} V, charging {(Telemetry.Rpm > 900 ? "OK" : "idle")}");
                }
            }
        }

        public static Color StatusColor(string status) => status switch
        {
            "Good" => Ui.Theme.Good,
            "Warning" => Ui.Theme.Warn,
            _ => Ui.Theme.Critical,
        };

        public static void Start()
        {
            Ui.Theme.Dark = Settings.DarkMode;
            if (!Settings.AutoConnect)
            {
                Connection.Disconnect();
            }

            Telemetry.Start();
        }

        public static void Shutdown()
        {
            Settings.Save();
            Telemetry.Dispose();
            Connection.Dispose();
        }
    }
}

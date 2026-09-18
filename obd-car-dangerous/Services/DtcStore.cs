namespace obd_car_dangerous.Services
{
    internal enum DtcStatus
    {
        Current,
        Pending,
        History,
    }

    internal enum AlarmLevel
    {
        Info,
        Warning,
        Critical,
    }

    internal sealed class DtcRecord
    {
        public required string Code { get; init; }

        public required string Description { get; init; }

        /// <summary>High, Medium or Low - drives the colour used everywhere.</summary>
        public required string Severity { get; init; }

        public DtcStatus Status { get; set; }

        public DateTime DetectedAt { get; set; } = DateTime.Now;

        public string[] Causes { get; init; } = Array.Empty<string>();

        public string Effect { get; init; } = string.Empty;

        public string System { get; init; } = "Engine";

        public Dictionary<string, string> FreezeFrame { get; init; } = new();

        public AlarmLevel Level => Severity switch
        {
            "High" => AlarmLevel.Critical,
            "Medium" => AlarmLevel.Warning,
            _ => AlarmLevel.Info,
        };
    }

    internal sealed record AlarmEntry(DateTime Time, string Code, string Description, AlarmLevel Level);

    /// <summary>Diagnostic trouble codes plus the alarm log shown on the history screen.</summary>
    internal sealed class DtcStore
    {
        private readonly List<DtcRecord> codes = new();
        private readonly List<AlarmEntry> alarms = new();

        public DtcStore()
        {
            Seed();
        }

        public event EventHandler? Changed;

        /// <summary>Raised when a new fault appears and the danger screen should be shown.</summary>
        public event EventHandler<DtcRecord>? DangerRaised;

        public IReadOnlyList<DtcRecord> All => codes;

        public IReadOnlyList<AlarmEntry> Alarms => alarms;

        public IEnumerable<DtcRecord> ByStatus(DtcStatus status) =>
            codes.Where(c => c.Status == status).OrderByDescending(c => c.DetectedAt);

        public int Count(DtcStatus status) => codes.Count(c => c.Status == status);

        public bool HasCritical => codes.Any(c => c.Status == DtcStatus.Current && c.Severity == "High");

        public void Add(DtcRecord record, bool raiseDanger = true)
        {
            DtcRecord? existing = codes.FirstOrDefault(c => c.Code == record.Code && c.Status == record.Status);
            if (existing is not null)
            {
                return;
            }

            codes.Add(record);
            alarms.Insert(0, new AlarmEntry(record.DetectedAt, record.Code, record.Description, record.Level));
            Changed?.Invoke(this, EventArgs.Empty);

            if (raiseDanger && record.Status == DtcStatus.Current)
            {
                DangerRaised?.Invoke(this, record);
            }
        }

        /// <summary>Clears current and pending codes; they stay readable under History.</summary>
        public int ClearAll()
        {
            int cleared = 0;
            foreach (DtcRecord code in codes.Where(c => c.Status != DtcStatus.History).ToList())
            {
                code.Status = DtcStatus.History;
                cleared++;
            }

            if (cleared > 0)
            {
                alarms.Insert(0, new AlarmEntry(DateTime.Now, "CLEAR", $"{cleared} fault code(s) cleared by user", AlarmLevel.Info));
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return cleared;
        }

        public void Clear(DtcRecord record)
        {
            if (record.Status == DtcStatus.History)
            {
                return;
            }

            record.Status = DtcStatus.History;
            alarms.Insert(0, new AlarmEntry(DateTime.Now, record.Code, $"{record.Code} cleared by user", AlarmLevel.Info));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Log(string code, string description, AlarmLevel level)
        {
            alarms.Insert(0, new AlarmEntry(DateTime.Now, code, description, level));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Re-reads the ECU. In this build it restores the demo fault set.</summary>
        public void Rescan()
        {
            codes.Clear();
            Seed();
            Log("SCAN", "Full system scan completed", AlarmLevel.Info);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public DtcRecord? Find(string code) => codes.FirstOrDefault(c => c.Code == code);

        private void Seed()
        {
            DateTime now = DateTime.Now;

            codes.AddRange(new[]
            {
                new DtcRecord
                {
                    Code = "P0101",
                    Description = "Mass Air Flow (MAF) Sensor Range/Performance Too Low",
                    Severity = "High",
                    Status = DtcStatus.Current,
                    DetectedAt = now.AddMinutes(-42),
                    System = "Engine",
                    Effect = "This may cause poor fuel economy, reduced power, or engine misfire.",
                    Causes = new[]
                    {
                        "Faulty MAF sensor",
                        "Wiring or connector issues",
                        "Vacuum leak",
                        "Engine control module (ECM) issue",
                    },
                    FreezeFrame = new Dictionary<string, string>
                    {
                        ["Engine RPM"] = "2 480 rpm",
                        ["Vehicle Speed"] = "64 km/h",
                        ["Coolant Temp"] = "91 °C",
                        ["Engine Load"] = "48 %",
                        ["Short Fuel Trim"] = "+9.4 %",
                    },
                },
                new DtcRecord
                {
                    Code = "P0420",
                    Description = "Catalyst System Efficiency Below Threshold (Bank 1)",
                    Severity = "High",
                    Status = DtcStatus.Current,
                    DetectedAt = now.AddHours(-16),
                    System = "Emission",
                    Effect = "Emission levels rise and the vehicle may fail an emissions test.",
                    Causes = new[]
                    {
                        "Worn catalytic converter",
                        "Faulty downstream O2 sensor",
                        "Exhaust leak before the catalyst",
                        "Engine running rich or lean",
                    },
                    FreezeFrame = new Dictionary<string, string>
                    {
                        ["Engine RPM"] = "1 920 rpm",
                        ["Vehicle Speed"] = "48 km/h",
                        ["Catalyst Temp"] = "612 °C",
                        ["O2 Sensor B1S2"] = "0.62 V",
                    },
                },
                new DtcRecord
                {
                    Code = "P0442",
                    Description = "Evaporative Emission Control System Leak (Small Leak)",
                    Severity = "Medium",
                    Status = DtcStatus.Current,
                    DetectedAt = now.AddHours(-22),
                    System = "Emission",
                    Effect = "Fuel vapour escapes to the atmosphere; fuel economy may drop slightly.",
                    Causes = new[]
                    {
                        "Loose or damaged fuel cap",
                        "Cracked EVAP hose",
                        "Faulty purge valve",
                        "Leaking charcoal canister",
                    },
                    FreezeFrame = new Dictionary<string, string>
                    {
                        ["EVAP Pressure"] = "-0.4 kPa",
                        ["Fuel Level"] = "58 %",
                        ["Ambient Temp"] = "27 °C",
                    },
                },
                new DtcRecord
                {
                    Code = "P0171",
                    Description = "System Too Lean (Bank 1)",
                    Severity = "Medium",
                    Status = DtcStatus.Pending,
                    DetectedAt = now.AddMinutes(-15),
                    System = "Engine",
                    Effect = "Rough idle and hesitation under acceleration.",
                    Causes = new[] { "Vacuum leak", "Dirty MAF sensor", "Weak fuel pump", "Clogged fuel filter" },
                    FreezeFrame = new Dictionary<string, string>
                    {
                        ["Short Fuel Trim"] = "+12.5 %",
                        ["Long Fuel Trim"] = "+9.8 %",
                        ["Engine RPM"] = "812 rpm",
                    },
                },
                new DtcRecord
                {
                    Code = "U0100",
                    Description = "Lost Communication With ECM/PCM 'A'",
                    Severity = "High",
                    Status = DtcStatus.History,
                    DetectedAt = now.AddDays(-2),
                    System = "Network",
                    Effect = "Intermittent loss of engine data on the CAN bus.",
                    Causes = new[] { "CAN bus wiring fault", "Loose ECM connector", "Low battery voltage" },
                },
                new DtcRecord
                {
                    Code = "P0301",
                    Description = "Cylinder 1 Misfire Detected",
                    Severity = "High",
                    Status = DtcStatus.History,
                    DetectedAt = now.AddDays(-4),
                    System = "Engine",
                    Effect = "Rough running and possible catalyst damage.",
                    Causes = new[] { "Worn spark plug", "Faulty ignition coil", "Low compression", "Clogged injector" },
                },
                new DtcRecord
                {
                    Code = "P0135",
                    Description = "O2 Sensor Heater Circuit (Bank 1 Sensor 1)",
                    Severity = "Medium",
                    Status = DtcStatus.History,
                    DetectedAt = now.AddDays(-6),
                    System = "Emission",
                    Effect = "Longer warm-up time and slightly higher emissions.",
                    Causes = new[] { "Failed O2 sensor heater", "Blown fuse", "Damaged wiring" },
                },
                new DtcRecord
                {
                    Code = "P0128",
                    Description = "Coolant Thermostat Below Regulating Temperature",
                    Severity = "Low",
                    Status = DtcStatus.History,
                    DetectedAt = now.AddDays(-9),
                    System = "Engine",
                    Effect = "Engine takes too long to reach operating temperature.",
                    Causes = new[] { "Stuck open thermostat", "Faulty coolant sensor", "Low coolant level" },
                },
                new DtcRecord
                {
                    Code = "B1318",
                    Description = "Battery Voltage Low",
                    Severity = "Medium",
                    Status = DtcStatus.History,
                    DetectedAt = now.AddDays(-11),
                    System = "Body",
                    Effect = "Modules may reset while cranking.",
                    Causes = new[] { "Ageing battery", "Weak alternator", "Corroded terminals" },
                },
            });

            alarms.Clear();
            foreach (DtcRecord code in codes.OrderByDescending(c => c.DetectedAt))
            {
                alarms.Add(new AlarmEntry(code.DetectedAt, code.Code, code.Description, code.Level));
            }
        }
    }
}

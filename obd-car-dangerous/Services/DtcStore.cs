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

        /// <summary>True when the list came from a real ECU rather than the demo seed.</summary>
        public bool FromVehicle { get; private set; }

        /// <summary>
        /// Drops the demo fault set as soon as a real car is attached, so no simulated code is
        /// ever shown next to a live one.
        /// </summary>
        public void EnterLiveMode()
        {
            if (FromVehicle)
            {
                return;
            }

            codes.Clear();
            alarms.Clear();
            FromVehicle = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Restores the demo fault set when the user switches back to simulated data.</summary>
        public void EnterDemoMode()
        {
            if (!FromVehicle && codes.Count > 0)
            {
                return;
            }

            codes.Clear();
            FromVehicle = false;
            Seed();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reads modes 03 (stored), 07 (pending) and 0A (permanent) from the ECU and replaces the
        /// list. Does nothing in demo mode, where the seeded codes stay.
        /// </summary>
        public async Task RefreshFromVehicleAsync()
        {
            Obd.ObdLink link = AppState.Connection.Link;
            if (!link.IsConnected)
            {
                return;
            }

            List<string> stored;
            List<string> pending;
            List<string> permanent;
            Dictionary<string, string> freeze;

            try
            {
                stored = await link.RequestAsync(elm => elm.ReadTroubleCodes("03")).ConfigureAwait(true);
                pending = await link.RequestAsync(elm => elm.ReadTroubleCodes("07")).ConfigureAwait(true);
                permanent = await link.RequestAsync(elm => elm.ReadTroubleCodes("0A")).ConfigureAwait(true);
                freeze = stored.Count > 0
                    ? await link.RequestAsync(ReadFreezeFrame).ConfigureAwait(true)
                    : new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Log("READ", $"Could not read fault codes: {ex.Message}", AlarmLevel.Warning);
                return;
            }

            var previous = codes.Where(c => c.Status != DtcStatus.History).Select(c => c.Code).ToHashSet();

            codes.Clear();
            FromVehicle = true;

            foreach (string code in stored)
            {
                codes.Add(Build(code, DtcStatus.Current, code == stored[0] ? freeze : new Dictionary<string, string>()));
            }

            foreach (string code in pending.Where(c => !stored.Contains(c)))
            {
                codes.Add(Build(code, DtcStatus.Pending, new Dictionary<string, string>()));
            }

            foreach (string code in permanent.Where(c => !stored.Contains(c) && !pending.Contains(c)))
            {
                codes.Add(Build(code, DtcStatus.History, new Dictionary<string, string>()));
            }

            foreach (DtcRecord record in codes.Where(c => c.Status != DtcStatus.History && !previous.Contains(c.Code)))
            {
                alarms.Insert(0, new AlarmEntry(record.DetectedAt, record.Code, record.Description, record.Level));
            }

            if (stored.Count == 0 && pending.Count == 0 && permanent.Count == 0)
            {
                Log("READ", "No fault codes stored in the ECU", AlarmLevel.Info);
            }

            Changed?.Invoke(this, EventArgs.Empty);

            // A freshly read critical fault still deserves the danger screen.
            DtcRecord? worst = codes.FirstOrDefault(c => c.Status == DtcStatus.Current && c.Severity == "High" && !previous.Contains(c.Code));
            if (worst is not null)
            {
                DangerRaised?.Invoke(this, worst);
            }
        }

        private static Dictionary<string, string> ReadFreezeFrame(Obd.Elm327 elm)
        {
            var frame = new Dictionary<string, string>();

            foreach ((byte pid, string label, Func<byte[], string> format) in Obd.ObdPids.FreezeFrame)
            {
                byte[]? data = elm.ReadFreezeFrame(pid);
                if (data is null || data.Length == 0)
                {
                    continue;
                }

                try
                {
                    frame[label] = format(data);
                }
                catch (Exception)
                {
                    // A short or malformed frame simply means that value is not stored.
                }
            }

            return frame;
        }

        /// <summary>Builds a record for a code read from the car, described from the dictionary.</summary>
        private static DtcRecord Build(string code, DtcStatus status, Dictionary<string, string> freeze) => new()
        {
            Code = code,
            Description = DtcCatalog.Find(code)?.Description ?? "Manufacturer specific code",
            Severity = SeverityOf(code),
            Status = status,
            System = SystemOf(code),
            Effect = DtcCatalog.FamilyOf(code),
            DetectedAt = DateTime.Now,
            FreezeFrame = freeze,
        };

        /// <summary>
        /// Severity is not part of OBD2, so it is derived from the code family: anything that can
        /// damage the engine or disable a safety system counts as high.
        /// </summary>
        internal static string SeverityOf(string code)
        {
            if (code.Length < 3)
            {
                return "Medium";
            }

            string prefix = code[..3].ToUpperInvariant();

            return code[0] switch
            {
                'U' => "High",
                'C' => "High",
                'B' => "Medium",
                _ => prefix switch
                {
                    "P03" => "High",
                    "P00" or "P01" or "P02" => "High",
                    "P07" or "P08" => "High",
                    "P04" or "P05" or "P06" or "P09" => "Medium",
                    _ => "Medium",
                },
            };
        }

        internal static string SystemOf(string code)
        {
            if (code.Length < 3)
            {
                return "Engine";
            }

            return code[0] switch
            {
                'U' => "Network",
                'C' => "ABS",
                'B' => "Body",
                _ => code[..3].ToUpperInvariant() switch
                {
                    "P04" => "Emission",
                    "P07" or "P08" => "Transmission",
                    _ => "Engine",
                },
            };
        }

        /// <summary>Clears codes: mode 04 on a real car, list shuffling in demo mode.</summary>
        public async Task<int> ClearAsync()
        {
            Obd.ObdLink link = AppState.Connection.Link;
            if (!link.IsConnected)
            {
                return ClearAll();
            }

            try
            {
                bool accepted = await link.RequestAsync(elm => elm.ClearTroubleCodes()).ConfigureAwait(true);
                if (!accepted)
                {
                    Log("CLEAR", "ECU refused the clear request (mode 04)", AlarmLevel.Warning);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Log("CLEAR", $"Clear failed: {ex.Message}", AlarmLevel.Warning);
                return 0;
            }

            int cleared = codes.Count(c => c.Status != DtcStatus.History);
            foreach (DtcRecord code in codes.Where(c => c.Status != DtcStatus.History).ToList())
            {
                code.Status = DtcStatus.History;
            }

            Log("CLEAR", Loc.T("alarms.cleared", cleared), AlarmLevel.Info);
            await RefreshFromVehicleAsync().ConfigureAwait(true);
            return cleared;
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
                alarms.Insert(0, new AlarmEntry(DateTime.Now, "CLEAR", Loc.T("alarms.cleared", cleared), AlarmLevel.Info));
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

        /// <summary>Re-reads the ECU, or restores the demo fault set when no car is connected.</summary>
        public async Task RescanAsync()
        {
            if (AppState.Connection.Link.IsConnected)
            {
                await RefreshFromVehicleAsync().ConfigureAwait(true);
                Log("SCAN", "Full system scan completed", AlarmLevel.Info);
                return;
            }

            codes.Clear();
            FromVehicle = false;
            Seed();
            Log("SCAN", "Full system scan completed (demo data)", AlarmLevel.Info);
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

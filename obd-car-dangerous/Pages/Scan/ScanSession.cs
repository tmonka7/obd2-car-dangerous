using obd_car_dangerous.Services;

namespace obd_car_dangerous.Pages.Scan
{
    internal enum ModuleState
    {
        Pending,
        Scanning,
        Passed,
        Warning,
        Fault,
    }

    /// <summary>
    /// One control module of the scan. <see cref="Tile"/> names the icon artwork (one of the seven the
    /// mock-up draws); <see cref="Systems"/> are the fault code groups that belong to it.
    /// </summary>
    internal sealed class ScanModule
    {
        public ScanModule(string key, string shortName, string tile, int address, string ecuId, float seconds, int responseMs, params string[] systems)
        {
            Key = key;
            ShortName = shortName;
            Tile = tile;
            Address = address;
            EcuId = ecuId;
            Seconds = seconds;
            TypicalResponseMs = responseMs;
            Systems = systems;
        }

        public string Key { get; }

        public string ShortName { get; }

        public string Tile { get; }

        public int Address { get; }

        /// <summary>Part number shown in demo mode; generic OBD2 cannot read it from other modules.</summary>
        public string EcuId { get; }

        /// <summary>How long the module takes in the scan animation.</summary>
        public float Seconds { get; }

        /// <summary>Answer time reported on the card once the module is done.</summary>
        public int TypicalResponseMs { get; }

        public string[] Systems { get; }

        public ModuleState State { get; set; }

        public float Progress { get; set; }

        public int ResponseMs { get; set; }

        public int Codes { get; set; }

        public TimeSpan Elapsed { get; set; }

        public string FullName => Loc.T($"scan.mod.{Key}");

        public string CardName => Loc.T($"scan.card.{Key}");
    }

    /// <summary>
    /// The full system scan: walks every module in turn, then reports each one against the fault codes
    /// read from the vehicle. Kept for the whole session so leaving the screen does not stop it.
    /// </summary>
    internal static class ScanSession
    {
        /// <summary>The seven modules on the first page of cards, in card order.</summary>
        public static readonly string[] Featured = { "ecm", "tcm", "abs", "srs", "bcm", "tpms", "hvac" };

        /// <summary>Modules in the order they are scanned.</summary>
        public static readonly IReadOnlyList<ScanModule> Modules = new List<ScanModule>
        {
            new("gw", "GW", "bcm", 0x19, "89111-33010", 1.1f, 14, "Network"),
            new("eps", "EPS", "tcm", 0x44, "89650-33350", 1.2f, 21, "Chassis"),
            new("ipc", "IPC", "bcm", 0x17, "83800-33Q70", 1.0f, 16),
            new("hvb", "HVB", "ecm", 0x2B, "89892-33040", 1.4f, 25),
            new("mg", "MG", "ecm", 0x2C, "89981-33130", 1.3f, 23),
            new("sks", "SKS", "bcm", 0x3B, "89990-33500", 0.9f, 11),
            new("immo", "IMMO", "bcm", 0x25, "89780-33080", 0.9f, 13),
            new("pas", "PAS", "tpms", 0x76, "89340-33180", 1.1f, 19),
            new("cam", "CAM", "srs", 0x6B, "8646C-33080", 1.0f, 22),
            new("rad", "RAD", "tpms", 0x13, "88210-33250", 1.2f, 17),
            new("aud", "AUD", "hvac", 0x5F, "86140-33D30", 1.0f, 15),
            new("seat", "SEAT", "srs", 0x36, "84070-33060", 0.8f, 10),
            new("ecm", "ECM", "ecm", 0x10, "89661-33Z40", 2.2f, 12, "Engine", "Emission"),
            new("tcm", "TCM", "tcm", 0x18, "89530-33A70", 1.8f, 18, "Transmission"),
            new("abs", "ABS", "abs", 0x01, "89541-33210", 2.4f, 15, "ABS"),
            new("srs", "SRS", "srs", 0x58, "89170-33K20", 1.6f, 14, "Airbag"),
            new("bcm", "BCM", "bcm", 0x40, "89221-33570", 1.5f, 16, "Body"),
            new("tpms", "TPMS", "tpms", 0x2A, "89769-33050", 1.2f, 20),
            new("hvac", "HVAC", "hvac", 0x6C, "88650-33N50", 1.3f, 17),
            new("lgt", "LGT", "bcm", 0x6E, "89908-33030", 0.9f, 12),
            new("dcm", "DCM", "bcm", 0x6F, "86741-33110", 1.0f, 24),
        };

        private static readonly System.Windows.Forms.Timer Clock = new() { Interval = 100 };
        private static DateTime started;
        private static DateTime moduleStarted;
        private static DateTime finishedAt;
        private static int current = -1;
        private static bool finished;
        private static TimeSpan? frozen;

        static ScanSession()
        {
            Clock.Tick += (_, _) => Step();
        }

        public static event EventHandler? Changed;

        public static bool Running => Clock.Enabled;

        public static bool Finished => finished;

        public static ScanModule Find(string key) => Modules.First(m => m.Key == key);

        /// <summary>
        /// The module on the right hand panel: the one being scanned, or once the scan is over the first
        /// one that reported a fault, then a warning, then simply the last one.
        /// </summary>
        public static ScanModule Current => finished
            ? Modules.FirstOrDefault(m => m.State == ModuleState.Fault)
                ?? Modules.FirstOrDefault(m => m.State == ModuleState.Warning)
                ?? Modules[^1]
            : Modules[Math.Clamp(current, 0, Modules.Count - 1)];

        public static int Completed => Modules.Count(m => m.State is ModuleState.Passed or ModuleState.Warning or ModuleState.Fault);

        public static float Fraction => Completed / (float)Modules.Count;

        public static TimeSpan Elapsed => frozen
            ?? (current < 0 ? TimeSpan.Zero : (finished ? finishedAt : DateTime.Now) - started);

        public static void Start()
        {
            foreach (ScanModule module in Modules)
            {
                module.State = ModuleState.Pending;
                module.Progress = 0f;
                module.Codes = 0;
                module.ResponseMs = 0;
                module.Elapsed = TimeSpan.Zero;
            }

            started = DateTime.Now;
            finished = false;
            frozen = null;
            current = 0;
            Begin(Modules[0]);
            Clock.Start();

            // Read the codes now so the results are ready when each module is reported.
            _ = AppState.Dtc.RescanAsync();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Freezes the scan at a given point; used to render the screen the way the mock-up shows it.</summary>
        public static void Preview(string scanningKey, float progress, TimeSpan elapsed, TimeSpan moduleElapsed)
        {
            Clock.Stop();
            int index = Modules.Select(m => m.Key).ToList().IndexOf(scanningKey);
            for (int i = 0; i < Modules.Count; i++)
            {
                ScanModule module = Modules[i];
                module.State = i < index ? ModuleState.Passed : i == index ? ModuleState.Scanning : ModuleState.Pending;
                module.Progress = i < index ? 1f : i == index ? progress : 0f;
                module.ResponseMs = i < index ? module.TypicalResponseMs : 0;
                module.Codes = 0;
                module.Elapsed = i == index ? moduleElapsed : TimeSpan.Zero;
            }

            current = index;
            finished = false;
            frozen = elapsed;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Jumps to the end of a scan, every module reported; used to render the results.</summary>
        public static void PreviewFinished()
        {
            Clock.Stop();
            foreach (ScanModule module in Modules)
            {
                module.Progress = 1f;
                module.Elapsed = TimeSpan.FromSeconds(module.Seconds);
                Report(module);
            }

            current = Modules.Count - 1;
            finished = true;
            frozen = TimeSpan.FromSeconds(Modules.Sum(m => m.Seconds));
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void Begin(ScanModule module)
        {
            module.State = ModuleState.Scanning;
            module.Progress = 0f;
            moduleStarted = DateTime.Now;
        }

        private static void Step()
        {
            if (current < 0 || current >= Modules.Count)
            {
                Clock.Stop();
                return;
            }

            ScanModule module = Modules[current];
            module.Elapsed = DateTime.Now - moduleStarted;
            module.Progress = Math.Min(1f, (float)module.Elapsed.TotalSeconds / module.Seconds);

            if (module.Progress >= 1f)
            {
                Report(module);
                if (current + 1 < Modules.Count)
                {
                    current++;
                    Begin(Modules[current]);
                }
                else
                {
                    Clock.Stop();
                    finished = true;
                    finishedAt = DateTime.Now;
                    AppState.MarkScanned();
                    if (AppState.Settings.AlertSound)
                    {
                        System.Media.SystemSounds.Asterisk.Play();
                    }
                }
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void Report(ScanModule module)
        {
            DtcRecord[] codes = AppState.Dtc.All
                .Where(c => c.Status != DtcStatus.History && module.Systems.Contains(c.System))
                .ToArray();

            module.Codes = codes.Length;
            module.State = codes.Length == 0 ? ModuleState.Passed
                : codes.Any(c => c.Severity == "High") ? ModuleState.Fault
                : ModuleState.Warning;
            module.ResponseMs = module.TypicalResponseMs;
        }
    }
}

namespace obd_car_dangerous.Services
{
    /// <summary>One readable OBD2 parameter: where it lives in the UI and how to read it.</summary>
    internal sealed record Pid(
        string Key,
        string Label,
        string Unit,
        float Min,
        float Max,
        string Group,
        Func<Telemetry, float> Read,
        string Format = "0");

    /// <summary>A rolling buffer of samples for one parameter.</summary>
    internal sealed class Series
    {
        private readonly float[] values;
        private int count;
        private int head;

        public Series(int capacity)
        {
            values = new float[capacity];
        }

        public int Count => count;

        public void Add(float value)
        {
            values[head] = value;
            head = (head + 1) % values.Length;
            if (count < values.Length)
            {
                count++;
            }
        }

        /// <summary>Most recent <paramref name="wanted"/> samples, oldest first.</summary>
        public float[] Recent(int wanted)
        {
            int take = Math.Min(wanted, count);
            var result = new float[take];
            for (int i = 0; i < take; i++)
            {
                int index = ((head - take + i) % values.Length + values.Length) % values.Length;
                result[i] = values[index];
            }

            return result;
        }
    }

    /// <summary>
    /// Simulated ELM327 data stream. Replace <see cref="Step"/> with real PID reads to drive the UI
    /// from an actual adapter - every screen reads its numbers from here.
    /// </summary>
    internal sealed class Telemetry : IDisposable
    {
        public const int TickMs = 200;
        private const int HistorySeconds = 600;

        private readonly System.Windows.Forms.Timer timer;
        private readonly Random random = new(20250907);
        private readonly Dictionary<string, Series> history = new();
        private readonly DateTime started = DateTime.Now;

        private float targetSpeed;
        private int phaseTicks;
        private float consumptionAccumulator = 12.4f;

        public Telemetry()
        {
            Rpm = 752;
            CoolantTemp = 86;
            IntakeTemp = 32;
            FuelLevel = 58;
            BatteryVoltage = 12.6f;
            BatterySoc = 68f;
            HighVoltage = 360f;
            HighCurrent = 12f;
            HighVoltageTemp = 34f;
            MotorRpm = 0f;
            AverageConsumption = 12.4f;
            DistanceKm = 56.8f;
            DrivingSeconds = 5520;
            MaxSpeed = 112;

            foreach (Pid pid in Pids)
            {
                history[pid.Key] = new Series(HistorySeconds * 1000 / TickMs);
            }

            timer = new System.Windows.Forms.Timer { Interval = TickMs };
            timer.Tick += (_, _) => Step();
        }

        public event EventHandler? Updated;

        /// <summary>Raised when a value crosses one of the configured alarm thresholds.</summary>
        public event EventHandler<string>? ThresholdExceeded;

        public bool Running => timer.Enabled;

        public float Rpm { get; private set; }

        public float Speed { get; private set; }

        public float CoolantTemp { get; private set; }

        public float IntakeTemp { get; private set; }

        public float EngineLoad { get; private set; } = 18;

        public float Throttle { get; private set; } = 12;

        public float MafRate { get; private set; } = 3.2f;

        public float ManifoldPressure { get; private set; } = 32;

        public float TimingAdvance { get; private set; } = 11;

        public float OilTemp { get; private set; } = 92;

        public float FuelLevel { get; private set; }

        public float FuelRate { get; private set; } = 0.9f;

        public float InstantConsumption { get; private set; } = 8.4f;

        public float AverageConsumption { get; private set; }

        public float FuelPressure { get; private set; } = 320;

        public float O2Voltage { get; private set; } = 0.45f;

        public float ShortFuelTrim { get; private set; } = 1.5f;

        public float LongFuelTrim { get; private set; } = -2.3f;

        public float CatalystTemp { get; private set; } = 452;

        public float EgrError { get; private set; } = -1.2f;

        public float EvapPressure { get; private set; } = -0.4f;

        public float BatteryVoltage { get; private set; }

        public float BatterySoc { get; private set; } = 68;

        public float HighVoltage { get; private set; } = 360;

        public float HighCurrent { get; private set; } = 12;

        public float HighVoltageTemp { get; private set; } = 34;

        public float MotorRpm { get; private set; } = 0;

        public float AmbientTemp { get; private set; } = 27;

        public float BarometricPressure { get; private set; } = 101;

        public float DistanceKm { get; private set; }

        public float MaxSpeed { get; private set; }

        public float DrivingSeconds { get; private set; }

        public float AvgSpeed => DrivingSeconds < 1 ? 0 : DistanceKm / (DrivingSeconds / 3600f);

        public TimeSpan Runtime => DateTime.Now - started;

        public static IReadOnlyList<Pid> Pids { get; } = new List<Pid>
        {
            new("rpm", "Engine RPM", "rpm", 0, 6000, "Engine", t => t.Rpm),
            new("speed", "Vehicle Speed", "km/h", 0, 220, "Engine", t => t.Speed),
            new("coolant", "Coolant Temp", "°C", 0, 130, "Engine", t => t.CoolantTemp),
            new("intake", "Intake Air Temp", "°C", 0, 90, "Engine", t => t.IntakeTemp),
            new("load", "Engine Load", "%", 0, 100, "Engine", t => t.EngineLoad),
            new("throttle", "Throttle Position", "%", 0, 100, "Engine", t => t.Throttle),

            new("maf", "MAF Air Flow", "g/s", 0, 120, "Sensors", t => t.MafRate, "0.0"),
            new("map", "Intake Manifold", "kPa", 0, 140, "Sensors", t => t.ManifoldPressure),
            new("o2", "O2 Sensor B1S1", "V", 0, 1.2f, "Sensors", t => t.O2Voltage, "0.00"),
            new("timing", "Timing Advance", "°", -20, 60, "Sensors", t => t.TimingAdvance),
            new("oil", "Oil Temp", "°C", 0, 150, "Sensors", t => t.OilTemp),
            new("battery", "Battery Voltage", "V", 0, 16, "Sensors", t => t.BatteryVoltage, "0.0"),
            new("soc", "Battery SOC", "%", 0, 100, "EV/Hybrid", t => t.BatterySoc),
            new("hv_voltage", "HV Pack Voltage", "V", 0, 600, "EV/Hybrid", t => t.HighVoltage, "0.0"),
            new("hv_current", "HV Pack Current", "A", -200, 200, "EV/Hybrid", t => t.HighCurrent, "0.0"),
            new("hv_temp", "HV Pack Temp", "°C", 0, 120, "EV/Hybrid", t => t.HighVoltageTemp),
            new("motor_rpm", "Motor RPM", "rpm", 0, 12000, "EV/Hybrid", t => t.MotorRpm),

            new("fuellevel", "Fuel Level", "%", 0, 100, "Fuel", t => t.FuelLevel),
            new("fuelrate", "Fuel Rate", "L/h", 0, 30, "Fuel", t => t.FuelRate, "0.0"),
            new("consumption", "Instant Economy", "L/100km", 0, 30, "Fuel", t => t.InstantConsumption, "0.0"),
            new("fuelpressure", "Fuel Pressure", "kPa", 0, 600, "Fuel", t => t.FuelPressure),
            new("stft", "Short Fuel Trim", "%", -25, 25, "Fuel", t => t.ShortFuelTrim, "0.0"),
            new("ltft", "Long Fuel Trim", "%", -25, 25, "Fuel", t => t.LongFuelTrim, "0.0"),

            new("catalyst", "Catalyst Temp", "°C", 0, 900, "Emission", t => t.CatalystTemp),
            new("egr", "EGR Error", "%", -30, 30, "Emission", t => t.EgrError, "0.0"),
            new("evap", "EVAP Pressure", "kPa", -5, 5, "Emission", t => t.EvapPressure, "0.0"),
            new("o2trim", "O2 Trim B1", "%", -25, 25, "Emission", t => t.ShortFuelTrim, "0.0"),

            new("ambient", "Ambient Temp", "°C", -20, 60, "Other", t => t.AmbientTemp),
            new("baro", "Barometric", "kPa", 80, 110, "Other", t => t.BarometricPressure),
            new("distance", "Trip Distance", "km", 0, 500, "Other", t => t.DistanceKm, "0.0"),
            new("runtime", "Engine Runtime", "min", 0, 240, "Other", t => (float)t.Runtime.TotalMinutes, "0.0"),
        };

        public static IReadOnlyList<string> Groups { get; } = new[] { "Engine", "Sensors", "Fuel", "Emission", "EV/Hybrid", "Other" };

        public static Pid Find(string key) => Pids.First(p => p.Key == key);

        public Series HistoryOf(string key) => history.TryGetValue(key, out Series? s) ? s : new Series(8);

        public float Value(string key) => Find(key).Read(this);

        /// <summary>Zeroes the trip computer.</summary>
        public void ResetTrip()
        {
            DistanceKm = 0;
            DrivingSeconds = 0;
            MaxSpeed = Speed;
            AppState.Dtc.Log("TRIP", "Trip data reset by user", AlarmLevel.Info);
            Updated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Runs the simulation without the timer, so charts have data in offscreen renders and tests.</summary>
        public void Warmup(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                Step();
            }
        }

        public void Start() => timer.Start();

        public void Stop() => timer.Stop();

        private void Step()
        {
            float dt = TickMs / 1000f;

            if (AppState.Connection.IsLive)
            {
                ReadLive();
            }
            else if (AppState.Connection.IsDemo)
            {
                Simulate(dt);
            }
            else
            {
                // No adapter: the engine data freezes, only the clock keeps moving.
                Updated?.Invoke(this, EventArgs.Empty);
                return;
            }

            Integrate(dt);
            CheckThresholds();
            Updated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Copies the latest PID values the worker thread read from the ECU.</summary>
        private void ReadLive()
        {
            Obd.ObdSnapshot snapshot = AppState.Connection.Link.Snapshot;

            Rpm = snapshot.Get("rpm") ?? Rpm;
            Speed = snapshot.Get("speed") ?? Speed;
            CoolantTemp = snapshot.Get("coolant") ?? CoolantTemp;
            IntakeTemp = snapshot.Get("intake") ?? IntakeTemp;
            EngineLoad = snapshot.Get("load") ?? EngineLoad;
            Throttle = snapshot.Get("throttle") ?? Throttle;
            MafRate = snapshot.Get("maf") ?? MafRate;
            ManifoldPressure = snapshot.Get("map") ?? ManifoldPressure;
            TimingAdvance = snapshot.Get("timing") ?? TimingAdvance;
            OilTemp = snapshot.Get("oil") ?? OilTemp;
            FuelLevel = snapshot.Get("fuellevel") ?? FuelLevel;
            FuelPressure = snapshot.Get("fuelpressure") ?? FuelPressure;
            O2Voltage = snapshot.Get("o2") ?? O2Voltage;
            ShortFuelTrim = snapshot.Get("stft") ?? ShortFuelTrim;
            LongFuelTrim = snapshot.Get("ltft") ?? LongFuelTrim;
            CatalystTemp = snapshot.Get("catalyst") ?? CatalystTemp;
            EgrError = snapshot.Get("egr") ?? EgrError;
            EvapPressure = snapshot.Get("evap") ?? EvapPressure;
            BatteryVoltage = snapshot.Get("battery") ?? BatteryVoltage;
            AmbientTemp = snapshot.Get("ambient") ?? AmbientTemp;
            BarometricPressure = snapshot.Get("baro") ?? BarometricPressure;

            // Fuel rate PID 0x5E is optional; air mass gives a good estimate when it is missing.
            FuelRate = snapshot.Get("fuelrate") ?? Math.Max(0f, MafRate * 3600f / (14.7f * 745f));
        }

        /// <summary>Drive cycle simulation used in demo mode.</summary>
        private void Simulate(float dt)
        {
            // Drive cycle: pick a new target speed every few seconds.
            if (--phaseTicks <= 0)
            {
                phaseTicks = random.Next(25, 90);
                targetSpeed = random.NextDouble() switch
                {
                    < 0.18 => 0,
                    < 0.45 => random.Next(25, 55),
                    < 0.8 => random.Next(55, 95),
                    _ => random.Next(95, 135),
                };
            }

            float accel = Math.Clamp(targetSpeed - Speed, -28f, 18f) * 0.35f;
            Speed = Math.Max(0, Speed + accel * dt + (float)(random.NextDouble() - 0.5) * 0.6f);

            int gear = Speed switch
            {
                < 18 => 1,
                < 34 => 2,
                < 52 => 3,
                < 78 => 4,
                < 105 => 5,
                _ => 6,
            };
            float gearRatio = gear switch { 1 => 48f, 2 => 30f, 3 => 22f, 4 => 17f, 5 => 13.5f, _ => 11.5f };
            float targetRpm = Speed < 1 ? 720 + (float)random.NextDouble() * 60 : Speed * gearRatio + accel * 40f;
            Rpm += (Math.Clamp(targetRpm, 650, 6000) - Rpm) * 0.25f;

            Throttle += (Math.Clamp(8 + accel * 4.5f + Speed * 0.22f, 4, 96) - Throttle) * 0.2f;
            EngineLoad += (Math.Clamp(14 + Throttle * 0.78f, 8, 98) - EngineLoad) * 0.18f;
            MafRate += (Math.Clamp(2.4f + EngineLoad * Rpm / 5200f * 0.9f, 1.8f, 115f) - MafRate) * 0.2f;
            ManifoldPressure += (Math.Clamp(26 + Throttle * 0.95f, 20, 135) - ManifoldPressure) * 0.2f;
            TimingAdvance += ((float)(8 + random.NextDouble() * 6 + Speed * 0.08f) - TimingAdvance) * 0.1f;

            float coolantTarget = 88 + EngineLoad * 0.12f;
            CoolantTemp += (coolantTarget - CoolantTemp) * 0.01f;
            OilTemp += (CoolantTemp + 6 - OilTemp) * 0.008f;
            IntakeTemp += (AmbientTemp + 5 + EngineLoad * 0.06f - IntakeTemp) * 0.01f;
            CatalystTemp += (380 + EngineLoad * 4.4f - CatalystTemp) * 0.02f;

            O2Voltage = 0.45f + (float)Math.Sin(Environment.TickCount / 260.0) * 0.32f;
            ShortFuelTrim += ((float)(random.NextDouble() * 6 - 3) - ShortFuelTrim) * 0.08f;
            LongFuelTrim += (-2.3f - LongFuelTrim) * 0.02f;
            EgrError += ((float)(random.NextDouble() * 4 - 2) - EgrError) * 0.05f;
            EvapPressure += ((float)(random.NextDouble() - 0.6) - EvapPressure) * 0.05f;
            BatteryVoltage += ((Rpm > 900 ? 14.2f : 12.5f) - BatteryVoltage) * 0.05f;
            FuelPressure += (300 + Throttle * 1.4f - FuelPressure) * 0.08f;
            BarometricPressure = 101;

            // Fuel: litres per hour derived from air mass, then economy.
            FuelRate = Math.Max(0.6f, MafRate / 14.7f / 0.745f * 3.6f);
            FuelLevel = Math.Max(0, FuelLevel - Speed * dt / 3600f * 0.09f);
        }

        /// <summary>Shared for live and demo data: economy, trip totals and the rolling history.</summary>
        private void Integrate(float dt)
        {
            InstantConsumption = Speed < 3 ? 0 : Math.Min(40f, FuelRate / Speed * 100f);
            if (Speed > 3)
            {
                consumptionAccumulator += (InstantConsumption - consumptionAccumulator) * 0.002f;
                AverageConsumption = consumptionAccumulator;
            }

            DistanceKm += Speed * dt / 3600f;
            DrivingSeconds += dt;
            MaxSpeed = Math.Max(MaxSpeed, Speed);

            foreach (Pid pid in Pids)
            {
                history[pid.Key].Add(pid.Read(this));
            }
        }

        private DateTime lastThresholdAlert = DateTime.MinValue;

        private void CheckThresholds()
        {
            if ((DateTime.Now - lastThresholdAlert).TotalSeconds < 20)
            {
                return;
            }

            AppSettings settings = AppState.Settings;
            string? message = null;

            if (settings.NotifyOverheat && CoolantTemp > settings.CoolantLimit)
            {
                message = $"Coolant temperature {CoolantTemp:0} °C exceeds the {settings.CoolantLimit} °C limit.";
            }
            else if (settings.NotifyOverSpeed && Speed > settings.SpeedLimit)
            {
                message = $"Vehicle speed {Speed:0} km/h exceeds the {settings.SpeedLimit} km/h limit.";
            }
            else if (Rpm > settings.RpmLimit)
            {
                message = $"Engine speed {Rpm:0} rpm exceeds the {settings.RpmLimit} rpm limit.";
            }

            if (message is not null)
            {
                lastThresholdAlert = DateTime.Now;
                ThresholdExceeded?.Invoke(this, message);
            }
        }

        public void Dispose() => timer.Dispose();
    }
}

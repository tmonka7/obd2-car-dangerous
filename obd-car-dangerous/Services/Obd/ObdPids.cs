namespace obd_car_dangerous.Services.Obd
{
    /// <summary>
    /// One live parameter as the ECU exposes it: which mode 01 PID to ask for, how to turn the
    /// bytes into a number, and how often it is worth asking.
    /// </summary>
    /// <param name="Key">Matches the key used by <see cref="Telemetry"/> and the UI.</param>
    /// <param name="Pid">Mode 01 PID number.</param>
    /// <param name="Decode">Bytes to value, or null when the answer is too short.</param>
    /// <param name="Rate">0 = every cycle, 1 = every 4th, 2 = every 20th.</param>
    internal sealed record ObdReading(string Key, byte Pid, Func<byte[], float?> Decode, int Rate);

    /// <summary>Standard SAE J1979 mode 01 PIDs used by the dashboard.</summary>
    internal static class ObdPids
    {
        public static readonly ObdReading[] All =
        {
            // Fast: the numbers that move while driving.
            new("rpm", 0x0C, b => b.Length >= 2 ? ((b[0] * 256f) + b[1]) / 4f : null, 0),
            new("speed", 0x0D, b => b.Length >= 1 ? b[0] : null, 0),
            new("throttle", 0x11, b => b.Length >= 1 ? b[0] * 100f / 255f : null, 0),
            new("load", 0x04, b => b.Length >= 1 ? b[0] * 100f / 255f : null, 0),
            new("maf", 0x10, b => b.Length >= 2 ? ((b[0] * 256f) + b[1]) / 100f : null, 0),

            // Medium: engine state that drifts over seconds.
            new("coolant", 0x05, b => b.Length >= 1 ? b[0] - 40f : null, 1),
            new("intake", 0x0F, b => b.Length >= 1 ? b[0] - 40f : null, 1),
            new("map", 0x0B, b => b.Length >= 1 ? b[0] : null, 1),
            new("timing", 0x0E, b => b.Length >= 1 ? (b[0] / 2f) - 64f : null, 1),
            new("stft", 0x06, b => b.Length >= 1 ? (b[0] - 128f) * 100f / 128f : null, 1),
            new("ltft", 0x07, b => b.Length >= 1 ? (b[0] - 128f) * 100f / 128f : null, 1),
            new("o2", 0x14, b => b.Length >= 1 ? b[0] / 200f : null, 1),

            // Slow: temperatures, levels and trims.
            new("oil", 0x5C, b => b.Length >= 1 ? b[0] - 40f : null, 2),
            new("fuellevel", 0x2F, b => b.Length >= 1 ? b[0] * 100f / 255f : null, 2),
            new("fuelrate", 0x5E, b => b.Length >= 2 ? ((b[0] * 256f) + b[1]) / 20f : null, 2),
            new("fuelpressure", 0x0A, b => b.Length >= 1 ? b[0] * 3f : null, 2),
            new("catalyst", 0x3C, b => b.Length >= 2 ? (((b[0] * 256f) + b[1]) / 10f) - 40f : null, 2),
            new("egr", 0x2D, b => b.Length >= 1 ? (b[0] - 128f) * 100f / 128f : null, 2),
            new("evap", 0x32, b => b.Length >= 2 ? ((short)((b[0] << 8) | b[1]) / 4f) / 1000f : null, 2),
            new("ambient", 0x46, b => b.Length >= 1 ? b[0] - 40f : null, 2),
            new("baro", 0x33, b => b.Length >= 1 ? b[0] : null, 2),
            new("battery", 0x42, b => b.Length >= 2 ? ((b[0] * 256f) + b[1]) / 1000f : null, 2),
            new("runtime", 0x1F, b => b.Length >= 2 ? ((b[0] * 256f) + b[1]) / 60f : null, 2),

            // EV / hybrid pack telemetry, useful for plug-in and full-hybrid vehicles.
            new("soc", 0x5B, b => b.Length >= 1 ? b[0] * 100f / 255f : null, 2),
            new("hv_voltage", 0x7A, b => b.Length >= 2 ? ((b[0] * 256f) + b[1]) / 10f : null, 2),
            new("hv_current", 0x7B, b => b.Length >= 2 ? (((short)((b[0] << 8) | b[1])) / 10f) : null, 2),
            new("hv_temp", 0x7C, b => b.Length >= 1 ? b[0] - 40f : null, 2),
            new("motor_rpm", 0x7D, b => b.Length >= 2 ? ((b[0] * 256f) + b[1]) : null, 2),
        };

        /// <summary>Wide range O2 sensor, used when the narrow band PID 0x14 is missing.</summary>
        public static readonly ObdReading WideBandO2 =
            new("o2", 0x24, b => b.Length >= 4 ? ((b[2] * 256f) + b[3]) * 8f / 65535f : null, 1);

        /// <summary>PIDs read from freeze frame when a fault code is opened.</summary>
        public static readonly (byte Pid, string Label, Func<byte[], string> Format)[] FreezeFrame =
        {
            (0x0C, "Engine RPM", b => $"{((b[0] * 256f) + b[1]) / 4f:0} rpm"),
            (0x0D, "Vehicle Speed", b => $"{b[0]} km/h"),
            (0x05, "Coolant Temp", b => $"{b[0] - 40} °C"),
            (0x04, "Engine Load", b => $"{b[0] * 100f / 255f:0} %"),
            (0x11, "Throttle Position", b => $"{b[0] * 100f / 255f:0} %"),
            (0x06, "Short Fuel Trim", b => $"{(b[0] - 128f) * 100f / 128f:+0.0;-0.0} %"),
        };
    }
}

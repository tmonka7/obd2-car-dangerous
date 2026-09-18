using System.Text.Json;
using System.Text.Json.Serialization;

namespace obd_car_dangerous.Services
{
    /// <summary>User preferences, persisted to %AppData%\ObdCarDangerous\settings.json.</summary>
    internal sealed class AppSettings
    {
        private static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ObdCarDangerous");

        private static readonly string FilePath = Path.Combine(Folder, "settings.json");

        public event EventHandler? Changed;

        public bool AutoConnect { get; set; } = true;

        /// <summary>Address of the adapter that last worked, tried first on the next start.</summary>
        public string LastAdapter { get; set; } = string.Empty;

        public bool AlertSound { get; set; } = true;

        public bool DarkMode { get; set; }

        public string Language { get; set; } = "English";

        public int ScreenTimeoutMinutes { get; set; } = 5;

        public bool KeepScreenOn { get; set; } = true;

        // Alerts
        public bool DangerPopup { get; set; } = true;

        public bool NotifyNewDtc { get; set; } = true;

        public bool NotifyOverheat { get; set; } = true;

        public bool NotifyOverSpeed { get; set; }

        public int SpeedLimit { get; set; } = 120;

        public int CoolantLimit { get; set; } = 110;

        public int RpmLimit { get; set; } = 5500;

        // Units
        public bool Metric { get; set; } = true;

        public string TemperatureUnit { get; set; } = "Celsius";

        public string PressureUnit { get; set; } = "kPa";

        public string ConsumptionUnit { get; set; } = "L/100km";

        [JsonIgnore]
        public string SpeedUnit => Metric ? "km/h" : "mph";

        [JsonIgnore]
        public string DistanceUnit => Metric ? "km" : "mi";

        [JsonIgnore]
        public string TempUnit => TemperatureUnit == "Celsius" ? "°C" : "°F";

        public float Speed(float kmh) => Metric ? kmh : kmh * 0.621371f;

        public float Distance(float km) => Metric ? km : km * 0.621371f;

        public float Temperature(float celsius) => TemperatureUnit == "Celsius" ? celsius : celsius * 9f / 5f + 32f;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                    if (loaded is not null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception)
            {
                // A corrupt or unreadable settings file must never stop the app from starting.
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception)
            {
                // Saving preferences is best effort.
            }
        }

        /// <summary>Applies a change, persists it and notifies listeners.</summary>
        public void Update(Action<AppSettings> change)
        {
            change(this);
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

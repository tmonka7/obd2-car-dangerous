using obd_car_dangerous.Services;

namespace obd_car_dangerous
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Lists every adapter the app can see, including a Bluetooth LE advertisement scan.
            if (args.Length > 0 && args[0] == "--devices")
            {
                string report = Path.Combine(Path.GetTempPath(), "obd-devices.txt");
                using var writer = new StreamWriter(report);

                writer.WriteLine("COM ports and paired Bluetooth devices:");
                foreach (Services.Obd.ObdEndpoint endpoint in Services.Obd.ObdLink.Discover())
                {
                    Describe(writer, endpoint);
                }

                writer.WriteLine();
                writer.WriteLine("Unpaired devices in range (Bluetooth LE advertisements + Classic inquiry):");
                foreach (Services.Obd.ObdEndpoint endpoint in
                         Services.Obd.ObdLink.ScanBluetoothAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult())
                {
                    Describe(writer, endpoint);
                }

                static void Describe(TextWriter log, Services.Obd.ObdEndpoint endpoint)
                {
                    Services.Obd.AdapterProfile profile = endpoint.Profile;
                    string usable = endpoint.Kind == Services.Obd.EndpointKind.Demo ? "demo"
                        : profile.SpeaksElm327 ? "ELM327" : "NOT ELM327";
                    log.WriteLine($"  {endpoint.Kind,-13} {endpoint.Name,-26} {usable,-10} {profile.Model}");
                    log.WriteLine($"                {endpoint.Address}");
                }

                writer.Flush();
                return;
            }

            // Checks the ELM327 parsing against canned adapter answers; no hardware needed.
            if (args.Length > 0 && args[0] == "--selftest")
            {
                string log = Path.Combine(Path.GetTempPath(), "obd-selftest.txt");
                using var writer = new StreamWriter(log);
                int failures = Services.Obd.ObdSelfTest.Run(writer);
                writer.Flush();
                Environment.ExitCode = failures;
                return;
            }

            // Developer aid: render every screen to PNG and exit, no window needed.
            if (args.Length > 0 && args[0] == "--render")
            {
                string folder = args.Length > 1 ? args[1] : "screens";
                int width = args.Length > 2 ? int.Parse(args[2]) : 1920;
                int height = args.Length > 3 ? int.Parse(args[3]) : 1080;
                bool dark = args.Contains("--dark");
                try
                {
                    PageRenderer.RenderAll(folder, width, height, dark);
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "obd-render-error.txt"), ex.ToString());
                    throw;
                }

                return;
            }

            AppState.Start();

            using (var splash = new SplashForm())
            {
                splash.ShowDialog();
            }

            Application.Run(new MainForm());
        }
    }
}

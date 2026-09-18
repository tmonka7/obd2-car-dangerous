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

            using (var splash = new Form1())
            {
                splash.ShowDialog();
            }

            Application.Run(new MainForm());
        }
    }
}

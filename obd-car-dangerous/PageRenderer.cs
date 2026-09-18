using obd_car_dangerous.Pages;
using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous
{
    /// <summary>
    /// Developer helper: renders every screen to PNG without showing a window.
    /// Run "obd-car-dangerous.exe --render &lt;folder&gt; [width] [height]" to check the layouts.
    /// </summary>
    internal static class PageRenderer
    {
        public static void RenderAll(string folder, int width, int height, bool dark)
        {
            Directory.CreateDirectory(folder);
            Theme.Dark = dark;
            AppState.Telemetry.Warmup(900);

            var shell = new HeadlessShell();
            (string Name, PageBase Page, object? Argument)[] screens =
            {
                ("01-home", new HomePage(), null),
                ("02-diagnostics", new DiagnosticsPage(), null),
                ("03-livedata", new LiveDataPage(), null),
                ("04-dtc-codes", new DtcCodesPage(), null),
                ("05-dtc-detail", new DtcDetailPage(), AppState.Dtc.Find("P0101")),
                ("06-danger-alert", new DangerOverlay("Danger Alert!", "P0101",
                    "This may cause poor fuel economy, reduced power, or engine misfire.", AppState.Dtc.Find("P0101")), null),
                ("07-live-graph", new LiveGraphPage(), "rpm"),
                ("08-fuel", new FuelPage(), null),
                ("09-trip", new TripPage(), null),
                ("10-alarms", new AlarmHistoryPage(), null),
                ("11-settings-general", new SettingsPage(), "general"),
                ("12-settings-connection", new SettingsPage(), "connection"),
                ("13-settings-alerts", new SettingsPage(), "alerts"),
                ("14-settings-units", new SettingsPage(), "units"),
                ("15-vehicle-info", new SettingsPage(), "vehicle"),
                ("16-about", new SettingsPage(), "about"),
                ("17-system-detail", new SystemDetailPage(), "Engine"),
            };

            var sidebar = new Sidebar { Shell = shell };
            int railWidth = (int)Math.Round(Sidebar.ExpandedWidth * (height / PageBase.DesignHeight));

            foreach ((string name, PageBase page, object? argument) in screens)
            {
                bool overlay = page is DangerOverlay;
                int pageWidth = overlay ? width : width - railWidth;

                page.Shell = shell;
                page.Size = new Size(pageWidth, height);
                page.OnEnter(argument);

                using var canvas = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    g.Clear(Theme.PageTop);
                }

                using var pageImage = new Bitmap(pageWidth, height);
                page.DrawToBitmap(pageImage, new Rectangle(0, 0, pageWidth, height));

                using (Graphics g = Graphics.FromImage(canvas))
                {
                    if (!overlay)
                    {
                        sidebar.Selected = SidebarKey(name);
                        sidebar.Size = new Size(railWidth, height);
                        using var railImage = new Bitmap(railWidth, height);
                        sidebar.DrawToBitmap(railImage, new Rectangle(0, 0, railWidth, height));
                        g.DrawImage(railImage, 0, 0);
                    }

                    g.DrawImage(pageImage, overlay ? 0 : railWidth, 0);
                }

                canvas.Save(Path.Combine(folder, $"{name}.png"), System.Drawing.Imaging.ImageFormat.Png);
                page.Dispose();
            }

            sidebar.Dispose();
            Console.WriteLine($"Rendered {screens.Length} screens to {folder}");
        }

        private static string SidebarKey(string name)
        {
            if (name.Contains("settings") || name.Contains("about") || name.Contains("vehicle"))
            {
                return "settings";
            }

            if (name.Contains("dtc"))
            {
                return "dtc";
            }

            if (name.Contains("live"))
            {
                return "livedata";
            }

            if (name.Contains("diagnostics") || name.Contains("system"))
            {
                return "diagnostics";
            }

            if (name.Contains("fuel"))
            {
                return "fuel";
            }

            if (name.Contains("trip"))
            {
                return "trip";
            }

            if (name.Contains("alarms"))
            {
                return "alarms";
            }

            return "home";
        }

        private sealed class HeadlessShell : IShell
        {
            public bool CanGoBack => true;

            public void Navigate(string page, object? argument = null)
            {
            }

            public void Back()
            {
            }

            public void RefreshShell()
            {
            }
        }
    }
}

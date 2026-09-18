using System.Runtime.InteropServices;
using obd_car_dangerous.Pages;
using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous
{
    /// <summary>
    /// Application shell: navigation rail on the left, one page at a time on the right and a
    /// full screen danger overlay on top. Runs borderless full screen; F11 toggles, Esc leaves it.
    /// </summary>
    internal sealed class MainForm : Form, IShell
    {
        private readonly Sidebar sidebar = new();
        private readonly Panel host = new() { Dock = DockStyle.Fill };
        private readonly Dictionary<string, PageBase> pages = new();
        private readonly List<(string Page, object? Argument)> stack = new();
        private readonly System.Windows.Forms.Timer faultTimer = new() { Interval = 30_000 };

        private DangerOverlay? overlay;
        private Rectangle windowedBounds = new(140, 90, 1360, 850);
        private bool fullScreen;

        public MainForm()
        {
            Text = "OBD2 Car Dangerous System";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 560);
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Theme.PageTop;
            Icon = SystemIcons.Application;

            host.BackColor = Theme.PageTop;
            sidebar.Dock = DockStyle.Left;
            sidebar.ItemSelected = key => Navigate(key);
            sidebar.ToggleRequested = () =>
            {
                sidebar.Collapsed = !sidebar.Collapsed;
                LayoutShell();
            };

            Controls.Add(host);
            Controls.Add(sidebar);

            AppState.Telemetry.Updated += OnTelemetryUpdated;
            AppState.Telemetry.ThresholdExceeded += (_, message) => ShowDanger("LIMIT", message, null);
            AppState.Dtc.Changed += (_, _) => RefreshShell();
            AppState.Dtc.DangerRaised += (_, record) => ShowDanger(record.Code, record.Effect, record);
            AppState.Settings.Changed += (_, _) =>
            {
                Theme.Dark = AppState.Settings.DarkMode;
                Loc.Set(AppState.Settings.Language);
                ApplyPowerRequest();
            };
            Loc.Changed += (_, _) => RefreshShell();
            Theme.Changed += (_, _) =>
            {
                BackColor = Theme.PageTop;
                host.BackColor = Theme.PageTop;
                RefreshShell();
            };

            faultTimer.Tick += (_, _) => MaybeInjectFault();
            faultTimer.Start();

            Load += (_, _) =>
            {
                EnterFullScreen();
                Navigate("home");
                ApplyPowerRequest();
            };
        }

        // ---- navigation ---------------------------------------------------

        public bool CanGoBack => stack.Count > 1;

        private PageBase? CurrentPage =>
            stack.Count > 0 && pages.TryGetValue(stack[^1].Page, out PageBase? page) ? page : null;

        public void Navigate(string page, object? argument = null)
        {
            if (stack.Count > 0 && stack[^1].Page == page && argument is null && overlay is null)
            {
                Show(page, null);
                return;
            }

            stack.Add((page, argument));
            if (stack.Count > 40)
            {
                stack.RemoveAt(0);
            }

            Show(page, argument);
        }

        public void Back()
        {
            if (!CanGoBack)
            {
                return;
            }

            stack.RemoveAt(stack.Count - 1);
            (string page, object? argument) = stack[^1];
            Show(page, argument);
        }

        public void RefreshShell()
        {
            sidebar.Invalidate();
            foreach (PageBase page in pages.Values.Where(p => p.Visible))
            {
                page.Invalidate();
            }

            overlay?.Invalidate();
        }

        private void Show(string key, object? argument)
        {
            PageBase page = GetPage(key);

            foreach (PageBase other in pages.Values)
            {
                if (!ReferenceEquals(other, page) && other.Visible)
                {
                    other.OnLeave();
                    other.Visible = false;
                }
            }

            page.Visible = true;
            page.BringToFront();
            page.OnEnter(argument);

            // Give the page focus so pages with a text field (the dictionary) receive typing at once.
            if (page.CanFocus)
            {
                page.Focus();
            }

            page.Invalidate();

            sidebar.Selected = RootOf(key);
            sidebar.Invalidate();
            overlay?.BringToFront();
        }

        private static string RootOf(string key) => key switch
        {
            "dtcdetail" => "dtc",
            "dictionary" => "dictionary",
            "graph" => "livedata",
            "systemdetail" => "diagnostics",
            "vehicleinfo" or "connection" or "about" => "settings",
            _ => key,
        };

        private PageBase GetPage(string key)
        {
            if (pages.TryGetValue(key, out PageBase? existing))
            {
                return existing;
            }

            PageBase page = key switch
            {
                "home" => new HomePage(),
                "diagnostics" => new DiagnosticsPage(),
                "systemdetail" => new SystemDetailPage(),
                "livedata" => new LiveDataPage(),
                "graph" => new LiveGraphPage(),
                "dtc" => new DtcCodesPage(),
                "dtcdetail" => new DtcDetailPage(),
                "dictionary" => new DictionaryPage(),
                "fuel" => new FuelPage(),
                "trip" => new TripPage(),
                "alarms" => new AlarmHistoryPage(),
                "settings" => new SettingsPage(),
                _ => new HomePage(),
            };

            page.Shell = this;
            page.Visible = false;
            pages[key] = page;
            host.Controls.Add(page);
            return page;
        }

        // ---- danger overlay ------------------------------------------------

        public void ShowDanger(string code, string message, DtcRecord? record)
        {
            if (!AppState.Settings.DangerPopup)
            {
                return;
            }

            overlay?.Dispose();
            overlay = new DangerOverlay(code, message, record)
            {
                Shell = this,
                Dock = DockStyle.Fill,
            };
            overlay.Closed += (_, _) =>
            {
                overlay?.Dispose();
                overlay = null;
                RefreshShell();
            };

            Controls.Add(overlay);
            overlay.BringToFront();
            overlay.Focus();

            if (AppState.Settings.AlertSound)
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
        }

        public bool DangerVisible => overlay is not null;

        public void CloseDanger() => overlay?.RequestClose();

        private void MaybeInjectFault()
        {
            if (!AppState.Settings.NotifyNewDtc || !AppState.Connection.IsConnected || overlay is not null)
            {
                return;
            }

            if (Random.Shared.NextDouble() > 0.12)
            {
                return;
            }

            (string code, string text, string severity, string system, string effect, string[] causes) candidate =
                Random.Shared.Next(3) switch
                {
                    0 => ("P0300", "Random/Multiple Cylinder Misfire Detected", "High", "Engine",
                        "Misfires can overheat and destroy the catalytic converter. Reduce speed and service soon.",
                        new[] { "Worn spark plugs", "Failing ignition coil", "Vacuum leak", "Low fuel pressure" }),
                    1 => ("P0128", "Coolant Thermostat Below Regulating Temperature", "Low", "Engine",
                        "The engine warms up too slowly, raising fuel use in cold weather.",
                        new[] { "Thermostat stuck open", "Faulty coolant temperature sensor" }),
                    _ => ("C0035", "Left Front Wheel Speed Sensor Circuit", "Medium", "ABS",
                        "ABS and traction control may be disabled; braking distance can increase.",
                        new[] { "Damaged wheel speed sensor", "Wiring harness fault", "Dirty sensor ring" }),
                };

            AppState.Dtc.Add(new DtcRecord
            {
                Code = candidate.code,
                Description = candidate.text,
                Severity = candidate.severity,
                Status = DtcStatus.Current,
                System = candidate.system,
                Effect = candidate.effect,
                Causes = candidate.causes,
                DetectedAt = DateTime.Now,
                FreezeFrame = new Dictionary<string, string>
                {
                    ["Engine RPM"] = $"{AppState.Telemetry.Rpm:0} rpm",
                    ["Vehicle Speed"] = $"{AppState.Telemetry.Speed:0} km/h",
                    ["Coolant Temp"] = $"{AppState.Telemetry.CoolantTemp:0} °C",
                    ["Engine Load"] = $"{AppState.Telemetry.EngineLoad:0} %",
                },
            });
        }

        // ---- full screen ----------------------------------------------------

        public bool IsFullScreen => fullScreen;

        public void ToggleFullScreen()
        {
            if (fullScreen)
            {
                LeaveFullScreen();
            }
            else
            {
                EnterFullScreen();
            }
        }

        private void EnterFullScreen()
        {
            if (!fullScreen && WindowState == FormWindowState.Normal)
            {
                windowedBounds = Bounds;
            }

            fullScreen = true;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = Screen.FromControl(this).Bounds;
            TopMost = false;
            LayoutShell();
        }

        private void LeaveFullScreen()
        {
            fullScreen = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = windowedBounds;
            LayoutShell();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutShell();
        }

        private void LayoutShell()
        {
            float scale = Math.Max(0.2f, ClientSize.Height / PageBase.DesignHeight);
            if (ClientSize.Width < 1180 && !sidebar.Collapsed)
            {
                sidebar.Collapsed = true;
            }

            sidebar.Width = (int)Math.Round((sidebar.Collapsed ? Sidebar.CollapsedWidth : Sidebar.ExpandedWidth) * scale);
            sidebar.Invalidate();
            host.Invalidate();
            foreach (PageBase page in pages.Values.Where(p => p.Visible))
            {
                page.Invalidate();
            }
        }

        // ---- input -----------------------------------------------------------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F11:
                    ToggleFullScreen();
                    return true;

                case Keys.Escape:
                    if (overlay is not null)
                    {
                        CloseDanger();
                    }
                    else if (fullScreen)
                    {
                        LeaveFullScreen();
                    }
                    else if (CanGoBack)
                    {
                        Back();
                    }

                    return true;

                case Keys.Alt | Keys.Left:
                    Back();
                    return true;

                case Keys.Back:
                    // The dictionary search field needs Backspace for itself.
                    if (CurrentPage?.WantsTextInput == true)
                    {
                        return base.ProcessCmdKey(ref msg, keyData);
                    }

                    Back();
                    return true;

                case Keys.Control | Keys.D:
                    AppState.Settings.Update(s => s.DarkMode = !s.DarkMode);
                    return true;

                case Keys.Control | Keys.Q:
                    Close();
                    return true;

                case Keys.F1:
                    Navigate("home");
                    return true;

                case Keys.F2:
                    Navigate("diagnostics");
                    return true;

                case Keys.F3:
                    Navigate("livedata");
                    return true;

                case Keys.F4:
                    Navigate("dtc");
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnTelemetryUpdated(object? sender, EventArgs e)
        {
            foreach (PageBase page in pages.Values.Where(p => p.Visible))
            {
                page.Invalidate();
            }

            sidebar.Invalidate();
            overlay?.Invalidate();
        }

        // ---- screen keep alive --------------------------------------------

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint flags);

        private const uint EsContinuous = 0x80000000;
        private const uint EsDisplayRequired = 0x00000002;

        private void ApplyPowerRequest() =>
            SetThreadExecutionState(AppState.Settings.KeepScreenOn ? EsContinuous | EsDisplayRequired : EsContinuous);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SetThreadExecutionState(EsContinuous);
            faultTimer.Stop();
            AppState.Shutdown();
            base.OnFormClosing(e);
        }
    }
}

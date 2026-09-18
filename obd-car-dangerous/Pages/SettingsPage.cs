using System.Drawing.Drawing2D;
using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Settings hub: general, connection, alerts, units, vehicle info and about.</summary>
    internal sealed class SettingsPage : PageBase
    {
        private static readonly (string Key, string LabelKey, string Icon)[] Sections =
        {
            ("general", "set.general", "settings"),
            ("connection", "set.connection", "bluetooth"),
            ("alerts", "set.alerts", "bell"),
            ("units", "set.units", "ruler"),
            ("vehicle", "set.vehicle", "car"),
            ("about", "set.about", "info"),
        };

        private static readonly int[] Timeouts = { 1, 5, 10, 30, 0 };

        private int section;
        private float rowY;

        public override string Title => Loc.T("set.title");

        public override bool ShowBack => true;

        public override void OnEnter(object? argument)
        {
            if (argument is string key)
            {
                int index = Array.FindIndex(Sections, s => s.Key == key);
                if (index >= 0)
                {
                    section = index;
                }
            }

            ScrollY = 0;
        }

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);

            float navW = Math.Min(300f, W * 0.26f);
            var nav = new RectangleF(pad, top + 18, navW, H - top - 18 - pad);
            DrawNav(g, nav);

            var panel = new RectangleF(nav.Right + 20, top + 18, W - nav.Right - 20 - pad, H - top - 18 - pad);
            Draw.Card(g, panel, 20f);

            var inner = new RectangleF(panel.X + 26, panel.Y + 22, panel.Width - 52, panel.Height - 44);
            switch (Sections[section].Key)
            {
                case "general":
                    DrawGeneral(g, inner);
                    break;
                case "connection":
                    DrawConnection(g, inner);
                    break;
                case "alerts":
                    DrawAlerts(g, inner);
                    break;
                case "units":
                    DrawUnits(g, inner);
                    break;
                case "vehicle":
                    DrawVehicle(g, inner);
                    break;
                default:
                    DrawAbout(g, inner);
                    break;
            }

            DrawModal(g);
        }

        private void DrawNav(Graphics g, RectangleF bounds)
        {
            Draw.CardShadow(g, bounds, 20f);
            Draw.GradientRounded(g, Theme.ShellTop, Theme.ShellBottom, bounds, 20f);

            float y = bounds.Y + 18;
            foreach ((string key, string labelKey, string icon) in Sections)
            {
                var row = new RectangleF(bounds.X + 12, y, bounds.Width - 24, 62);
                bool active = key == Sections[section].Key;
                string id = $"set-nav-{key}";

                if (active)
                {
                    Draw.FillRounded(g, Theme.Accent, row, 13f);
                }
                else if (IsHover(id))
                {
                    Draw.FillRounded(g, Draw.Alpha(Color.White, 30), row, 13f);
                }

                Color knockout = active ? Theme.Accent : Theme.ShellTop;
                Icons.Draw(g, icon, new RectangleF(row.X + 16, row.Y + 17, 28, 28), active ? Color.White : Theme.ShellText, knockout);
                Draw.TextIn(g, Loc.T(labelKey), Draw.Font(20, active ? FontStyle.Bold : FontStyle.Regular),
                    active ? Color.White : Theme.ShellText,
                    new RectangleF(row.X + 56, row.Y, row.Width - 66, row.Height), StringAlignment.Near, StringAlignment.Center, false);

                int index = Array.FindIndex(Sections, s => s.Key == key);
                Hit(row, () =>
                {
                    section = index;
                    ScrollY = 0;
                    Invalidate();
                }, id);

                y += 68;
            }
        }

        // ---- rows ---------------------------------------------------------

        private void SectionTitle(Graphics g, RectangleF bounds, string title, string? subtitle = null)
        {
            Draw.Text(g, title, Draw.Font(28, FontStyle.Bold), Theme.Text, bounds.X, bounds.Y);
            if (subtitle is not null)
            {
                Draw.Text(g, subtitle, Draw.Font(18), Theme.TextSoft, bounds.X, bounds.Y + 38);
            }

            rowY = bounds.Y + (subtitle is null ? 54 : 78);
        }

        private RectangleF NextRow(RectangleF bounds, float height = 66f)
        {
            var row = new RectangleF(bounds.X, rowY, bounds.Width, height);
            rowY += height + 8;
            return row;
        }

        private void ToggleRow(Graphics g, RectangleF bounds, string label, string hint, bool value, Action<bool> set, string id)
        {
            RectangleF row = NextRow(bounds);
            Draw.FillRounded(g, IsHover(id) ? Theme.CardAlt : Draw.Alpha(Theme.CardAlt, Theme.Dark ? 255 : 140), row, 13f);

            Draw.TextIn(g, label, Draw.Font(22), Theme.Text,
                new RectangleF(row.X + 20, row.Y + (hint.Length > 0 ? 8 : 0), row.Width * 0.6f, hint.Length > 0 ? 32 : row.Height),
                StringAlignment.Near, StringAlignment.Center, false);

            if (hint.Length > 0)
            {
                Draw.TextIn(g, hint, Draw.Font(16), Theme.TextSoft,
                    new RectangleF(row.X + 20, row.Y + 36, row.Width * 0.72f, 24), StringAlignment.Near, StringAlignment.Center, false);
            }

            var toggle = new RectangleF(row.Right - 94, row.Y + (row.Height - 40) / 2f, 76, 40);
            Draw.ToggleSwitch(g, toggle, value);
            Hit(row, () => set(!value), id);
        }

        private void ValueRow(Graphics g, RectangleF bounds, string label, string value, Action onClick, string id)
        {
            RectangleF row = NextRow(bounds);
            Draw.FillRounded(g, IsHover(id) ? Theme.CardAlt : Draw.Alpha(Theme.CardAlt, Theme.Dark ? 255 : 140), row, 13f);

            Draw.TextIn(g, label, Draw.Font(22), Theme.Text,
                new RectangleF(row.X + 20, row.Y, row.Width * 0.5f, row.Height), StringAlignment.Near, StringAlignment.Center, false);
            Draw.TextIn(g, value, Draw.Font(22, FontStyle.Bold), Theme.Accent,
                new RectangleF(row.X + row.Width * 0.5f, row.Y, row.Width * 0.5f - 46, row.Height), StringAlignment.Far, StringAlignment.Center, false);
            Draw.Chevron(g, new PointF(row.Right - 26, row.Y + row.Height / 2f), 10f, Theme.TextSoft);

            Hit(row, onClick, id);
        }

        private void StepperRow(Graphics g, RectangleF bounds, string label, string value, Action minus, Action plus, string id)
        {
            RectangleF row = NextRow(bounds);
            Draw.FillRounded(g, Draw.Alpha(Theme.CardAlt, Theme.Dark ? 255 : 140), row, 13f);

            Draw.TextIn(g, label, Draw.Font(22), Theme.Text,
                new RectangleF(row.X + 20, row.Y, row.Width * 0.5f, row.Height), StringAlignment.Near, StringAlignment.Center, false);

            var minusBox = new RectangleF(row.Right - 190, row.Y + (row.Height - 42) / 2f, 42, 42);
            var plusBox = new RectangleF(row.Right - 62, minusBox.Y, 42, 42);

            Draw.FillRounded(g, Hovered(Theme.Accent, id + "-minus"), minusBox, 11f);
            Draw.TextCentered(g, "−", Draw.Font(26, FontStyle.Bold), Color.White, minusBox);
            Draw.FillRounded(g, Hovered(Theme.Accent, id + "-plus"), plusBox, 11f);
            Draw.TextCentered(g, "+", Draw.Font(26, FontStyle.Bold), Color.White, plusBox);

            Draw.TextCentered(g, value, Draw.Font(22, FontStyle.Bold), Theme.Text,
                new RectangleF(minusBox.Right, row.Y, plusBox.X - minusBox.Right, row.Height));

            Hit(minusBox, minus, id + "-minus");
            Hit(plusBox, plus, id + "-plus");
        }

        // ---- sections -----------------------------------------------------

        private void DrawGeneral(Graphics g, RectangleF bounds)
        {
            AppSettings s = AppState.Settings;
            SectionTitle(g, bounds, Loc.T("set.general.title"));

            ToggleRow(g, bounds, Loc.T("set.autoconnect"), Loc.T("set.autoconnect.hint"), s.AutoConnect,
                value => s.Update(x => x.AutoConnect = value), "set-auto");

            ToggleRow(g, bounds, Loc.T("set.alertsound"), Loc.T("set.alertsound.hint"), s.AlertSound,
                value => s.Update(x => x.AlertSound = value), "set-sound");

            ToggleRow(g, bounds, Loc.T("set.darkmode"), Loc.T("set.darkmode.hint"), s.DarkMode,
                value => s.Update(x => x.DarkMode = value), "set-dark");

            ToggleRow(g, bounds, Loc.T("set.keepscreen"), Loc.T("set.keepscreen.hint"), s.KeepScreenOn,
                value => s.Update(x => x.KeepScreenOn = value), "set-keep");

            ValueRow(g, bounds, Loc.T("set.language"), s.Language, () => s.Update(x =>
            {
                int index = Array.IndexOf(Loc.Languages, x.Language);
                x.Language = Loc.Languages[(index + 1) % Loc.Languages.Length];
            }), "set-lang");

            ValueRow(g, bounds, Loc.T("set.timeout"), s.ScreenTimeoutMinutes == 0 ? Loc.T("set.timeout.never") : Loc.T("set.timeout.minutes", s.ScreenTimeoutMinutes), () => s.Update(x =>
            {
                int index = Array.IndexOf(Timeouts, x.ScreenTimeoutMinutes);
                x.ScreenTimeoutMinutes = Timeouts[(index + 1) % Timeouts.Length];
            }), "set-timeout");

            var full = new RectangleF(bounds.X, rowY + 10, 280, 56);
            DrawGhostButton(g, full, Loc.T(Shell is MainForm { IsFullScreen: true } ? "set.fullscreen.leave" : "set.fullscreen.enter"),
                Theme.Accent, () => (Shell as MainForm)?.ToggleFullScreen(), "set-full", 13f);

            Draw.TextIn(g, Loc.T("set.shortcuts"),
                Draw.Font(17), Theme.TextSoft,
                new RectangleF(full.Right + 20, full.Y, bounds.Width - full.Width - 20, 56), StringAlignment.Near, StringAlignment.Center, false);
        }

        private void DrawConnection(Graphics g, RectangleF bounds)
        {
            ConnectionService link = AppState.Connection;
            SectionTitle(g, bounds, Loc.T("set.connection"));

            var hero = new RectangleF(bounds.X, rowY, bounds.Width, 250);
            Draw.FillRounded(g, Draw.Alpha(Theme.CardAlt, Theme.Dark ? 255 : 150), hero, 18f);

            bool connected = link.IsConnected;
            Color color = connected ? Theme.Good : link.State == Services.LinkState.Connecting ? Theme.Warn : Theme.TextSoft;

            var circle = new RectangleF(hero.X + hero.Width / 2f - 58, hero.Y + 24, 116, 116);
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, circle);
            }

            if (connected)
            {
                Draw.CheckMark(g, circle, Color.White, 9f);
            }
            else
            {
                Icons.Draw(g, "bluetooth", RectangleF.Inflate(circle, -34, -34), Color.White, color);
            }

            Draw.TextCentered(g, link.StatusText, Draw.Font(34, FontStyle.Bold), Theme.Text,
                new RectangleF(hero.X, circle.Bottom + 10, hero.Width, 44));
            Draw.TextCentered(g, $"{link.Current.Name} ({link.Current.Transport})", Draw.Font(21), Theme.TextSoft,
                new RectangleF(hero.X, circle.Bottom + 54, hero.Width, 30));
            Draw.TextCentered(g, connected ? Loc.T("conn.protocol", link.Protocol) : Loc.T("conn.nolink"), Draw.Font(21), Theme.TextSoft,
                new RectangleF(hero.X, circle.Bottom + 84, hero.Width, 30));

            rowY = hero.Bottom + 16;

            var buttons = new RectangleF(bounds.X, rowY, bounds.Width, 60);
            float half = (buttons.Width - 16) / 2f;
            if (connected)
            {
                DrawGhostButton(g, new RectangleF(buttons.X, buttons.Y, half, buttons.Height), Loc.T("conn.disconnect"), Theme.Critical,
                    () => link.Disconnect(), "conn-disconnect");
            }
            else
            {
                DrawGhostButton(g, new RectangleF(buttons.X, buttons.Y, half, buttons.Height), Loc.T("conn.connect"), Theme.Good,
                    () => link.Connect(), "conn-connect");
            }

            DrawButton(g, new RectangleF(buttons.X + half + 16, buttons.Y, half, buttons.Height),
                link.Scanning ? Loc.T("conn.scanning") : Loc.T("conn.scan"), Theme.Accent, Color.White, () => link.Scan(), "conn-scan");

            rowY = buttons.Bottom + 18;

            Draw.Text(g, Loc.T("conn.available"), Draw.Font(20, FontStyle.Bold), Theme.TextSoft, bounds.X, rowY);
            rowY += 34;

            foreach (Adapter adapter in link.Found)
            {
                RectangleF row = NextRow(bounds, 62);
                if (row.Bottom > bounds.Bottom - 40)
                {
                    break;
                }

                string id = $"adapter-{adapter.Address}";
                bool current = adapter.Address == link.Current.Address;
                Draw.FillRounded(g, IsHover(id) ? Theme.CardAlt : Draw.Alpha(Theme.CardAlt, Theme.Dark ? 255 : 140), row, 12f);

                Icons.Draw(g, adapter.Transport.StartsWith("Wi") ? "wifi" : "bluetooth",
                    new RectangleF(row.X + 16, row.Y + 16, 30, 30), current && connected ? Theme.Good : Theme.TextSoft, Theme.CardAlt);

                Draw.TextIn(g, adapter.Name, Draw.Font(21, FontStyle.Bold), Theme.Text,
                    new RectangleF(row.X + 58, row.Y, 260, row.Height), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, adapter.Address, Draw.Font(17), Theme.TextSoft,
                    new RectangleF(row.X + 320, row.Y, row.Width - 460, row.Height), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, current && connected ? Loc.T("state.connected") : Loc.T("conn.tap"), Draw.Font(17, FontStyle.Bold),
                    current && connected ? Theme.Good : Theme.Accent,
                    new RectangleF(row.Right - 200, row.Y, 184, row.Height), StringAlignment.Far, StringAlignment.Center, false);

                Adapter captured = adapter;
                Hit(row, () => link.Connect(captured), id);
            }

            if (connected)
            {
                Draw.TextIn(g, Loc.T("conn.signal", link.SignalStrength, link.Firmware), Draw.Font(17), Theme.TextSoft,
                    new RectangleF(bounds.X, bounds.Bottom - 30, bounds.Width, 28), StringAlignment.Near, StringAlignment.Center, false);
            }
        }

        private void DrawAlerts(Graphics g, RectangleF bounds)
        {
            AppSettings s = AppState.Settings;
            SectionTitle(g, bounds, Loc.T("alerts.title"));

            ToggleRow(g, bounds, Loc.T("alert.popup"), Loc.T("alert.popup.hint"),
                s.DangerPopup, value => s.Update(x => x.DangerPopup = value), "alert-popup");

            ToggleRow(g, bounds, Loc.T("alert.newdtc"), Loc.T("alert.newdtc.hint"),
                s.NotifyNewDtc, value => s.Update(x => x.NotifyNewDtc = value), "alert-dtc");

            ToggleRow(g, bounds, Loc.T("alert.overheat"), Loc.T("alert.overheat.hint"),
                s.NotifyOverheat, value => s.Update(x => x.NotifyOverheat = value), "alert-heat");

            ToggleRow(g, bounds, Loc.T("alert.overspeed"), Loc.T("alert.overspeed.hint"),
                s.NotifyOverSpeed, value => s.Update(x => x.NotifyOverSpeed = value), "alert-speed");

            StepperRow(g, bounds, Loc.T("alert.speedlimit"), $"{s.SpeedLimit} km/h",
                () => s.Update(x => x.SpeedLimit = Math.Max(40, x.SpeedLimit - 10)),
                () => s.Update(x => x.SpeedLimit = Math.Min(240, x.SpeedLimit + 10)), "alert-speedlimit");

            StepperRow(g, bounds, Loc.T("alert.coolantlimit"), $"{s.CoolantLimit} °C",
                () => s.Update(x => x.CoolantLimit = Math.Max(90, x.CoolantLimit - 5)),
                () => s.Update(x => x.CoolantLimit = Math.Min(130, x.CoolantLimit + 5)), "alert-coolant");

            StepperRow(g, bounds, Loc.T("alert.rpmlimit"), $"{s.RpmLimit} rpm",
                () => s.Update(x => x.RpmLimit = Math.Max(3000, x.RpmLimit - 250)),
                () => s.Update(x => x.RpmLimit = Math.Min(7500, x.RpmLimit + 250)), "alert-rpm");

            var test = new RectangleF(bounds.X, rowY + 8, 300, 56);
            DrawGhostButton(g, test, Loc.T("alert.test"), Theme.Critical, () =>
                (Shell as MainForm)?.ShowDanger("TEST", Loc.T("alert.test.body"), null),
                "alert-test", 13f);
        }

        private void DrawUnits(Graphics g, RectangleF bounds)
        {
            AppSettings s = AppState.Settings;
            SectionTitle(g, bounds, Loc.T("set.units"));

            ValueRow(g, bounds, Loc.T("units.system"), Loc.T(s.Metric ? "units.metric" : "units.imperial"),
                () => s.Update(x => x.Metric = !x.Metric), "unit-system");

            ValueRow(g, bounds, Loc.T("units.temperature"), Loc.T(s.TemperatureUnit == "Celsius" ? "units.celsius" : "units.fahrenheit"),
                () => s.Update(x => x.TemperatureUnit = x.TemperatureUnit == "Celsius" ? "Fahrenheit" : "Celsius"), "unit-temp");

            ValueRow(g, bounds, Loc.T("units.pressure"), s.PressureUnit, () => s.Update(x =>
            {
                string[] options = { "kPa", "bar", "psi" };
                x.PressureUnit = options[(Array.IndexOf(options, x.PressureUnit) + 1) % options.Length];
            }), "unit-pressure");

            ValueRow(g, bounds, Loc.T("units.consumption"), s.ConsumptionUnit, () => s.Update(x =>
            {
                string[] options = { "L/100km", "km/L", "MPG" };
                x.ConsumptionUnit = options[(Array.IndexOf(options, x.ConsumptionUnit) + 1) % options.Length];
            }), "unit-consumption");

            Draw.TextIn(g, Loc.T("units.note"),
                Draw.Font(18), Theme.TextSoft, new RectangleF(bounds.X, rowY + 14, bounds.Width, 60));

            var reset = new RectangleF(bounds.X, rowY + 80, 300, 56);
            DrawGhostButton(g, reset, Loc.T("units.reset"), Theme.TextSoft, () => OpenModal(
                Loc.T("units.reset.title"),
                Loc.T("units.reset.body"),
                Loc.T("units.reset.ok"), Theme.Critical, () =>
                {
                    var defaults = new AppSettings();
                    s.Update(x =>
                    {
                        x.AutoConnect = defaults.AutoConnect;
                        x.AlertSound = defaults.AlertSound;
                        x.DarkMode = defaults.DarkMode;
                        x.KeepScreenOn = defaults.KeepScreenOn;
                        x.Language = defaults.Language;
                        x.ScreenTimeoutMinutes = defaults.ScreenTimeoutMinutes;
                        x.DangerPopup = defaults.DangerPopup;
                        x.NotifyNewDtc = defaults.NotifyNewDtc;
                        x.NotifyOverheat = defaults.NotifyOverheat;
                        x.NotifyOverSpeed = defaults.NotifyOverSpeed;
                        x.SpeedLimit = defaults.SpeedLimit;
                        x.CoolantLimit = defaults.CoolantLimit;
                        x.RpmLimit = defaults.RpmLimit;
                        x.Metric = defaults.Metric;
                        x.TemperatureUnit = defaults.TemperatureUnit;
                        x.PressureUnit = defaults.PressureUnit;
                        x.ConsumptionUnit = defaults.ConsumptionUnit;
                    });
                }), "unit-reset", 13f);
        }

        private void DrawVehicle(Graphics g, RectangleF bounds)
        {
            SectionTitle(g, bounds, Loc.T("vehicle.title"), Loc.T("vehicle.subtitle"));

            var card = new RectangleF(bounds.X, rowY, bounds.Width, 210);
            Draw.FillRounded(g, Draw.Alpha(Theme.CardAlt, Theme.Dark ? 255 : 150), card, 16f);

            var iconBox = new RectangleF(card.X + 28, card.Y + 52, 120, 104);
            Icons.Draw(g, "car", iconBox, Theme.Accent, Theme.CardAlt);

            (string Label, string Value)[] identity =
            {
                (Loc.T("vehicle.make"), Vehicle.Make),
                (Loc.T("vehicle.model"), Vehicle.Model),
                (Loc.T("vehicle.year"), Vehicle.Year),
                (Loc.T("vehicle.vin"), Vehicle.Vin),
            };

            float y = card.Y + 26;
            foreach ((string label, string value) in identity)
            {
                Draw.TextIn(g, label, Draw.Font(21), Theme.TextSoft,
                    new RectangleF(iconBox.Right + 40, y, 180, 36), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, value, Draw.Font(22, FontStyle.Bold), Theme.Text,
                    new RectangleF(iconBox.Right + 230, y, card.Width - 300, 36), StringAlignment.Near, StringAlignment.Center, false);
                y += 42;
            }

            rowY = card.Bottom + 16;

            (string Label, string Value)[] ecu =
            {
                (Loc.T("vehicle.protocol"), AppState.Connection.Protocol),
                (Loc.T("vehicle.ecu"), Vehicle.EcuVersion),
                (Loc.T("vehicle.calibration"), Vehicle.CalibrationId),
                (Loc.T("vehicle.engine"), Vehicle.Engine),
                (Loc.T("vehicle.adapter"), $"{AppState.Connection.Current.Name} · {AppState.Connection.Firmware}"),
            };

            foreach ((string label, string value) in ecu)
            {
                RectangleF row = NextRow(bounds, 58);
                if (row.Bottom > bounds.Bottom)
                {
                    break;
                }

                Draw.TextIn(g, label, Draw.Font(21), Theme.TextSoft,
                    new RectangleF(row.X + 4, row.Y, row.Width * 0.5f, row.Height), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, value, Draw.Font(21, FontStyle.Bold), Theme.Text,
                    new RectangleF(row.X + row.Width * 0.45f, row.Y, row.Width * 0.55f - 4, row.Height), StringAlignment.Far, StringAlignment.Center, false);

                using var pen = new Pen(Theme.Border, 1f);
                g.DrawLine(pen, row.X, row.Bottom, row.Right, row.Bottom);
            }
        }

        private void DrawAbout(Graphics g, RectangleF bounds)
        {
            SectionTitle(g, bounds, Loc.T("set.about"));

            var card = new RectangleF(bounds.X, rowY, bounds.Width, 190);
            Draw.FillRounded(g, Draw.Alpha(Theme.CardAlt, Theme.Dark ? 255 : 150), card, 16f);

            var logo = new RectangleF(card.X + 30, card.Y + 42, 108, 108);
            Icons.Draw(g, "car", logo, Theme.Accent, Theme.CardAlt);
            Draw.WarningTriangle(g, new RectangleF(logo.Right - 42, logo.Bottom - 46, 46, 40), Theme.Critical, Color.White);

            Draw.Text(g, Loc.T("app.title"), Draw.Font(30, FontStyle.Bold), Theme.Text, logo.Right + 34, card.Y + 38);
            Draw.Text(g, Loc.T("about.version", "1.0.0"), Draw.Font(21), Theme.TextSoft, logo.Right + 34, card.Y + 80);
            Draw.TextIn(g, Loc.T("about.tagline"), Draw.Font(20), Theme.TextSoft,
                new RectangleF(logo.Right + 34, card.Y + 112, card.Width - (logo.Right - card.X) - 60, 60));

            rowY = card.Bottom + 18;

            (string Label, string Value)[] rows =
            {
                (Loc.T("about.build"), $".NET {Environment.Version} · Windows Forms"),
                (Loc.T("about.adapters"), "ELM327 over Bluetooth, Wi-Fi and USB"),
                (Loc.T("about.protocols"), "ISO 15765-4 CAN, ISO 9141-2, KWP2000, J1850"),
                (Loc.T("about.shortcuts"), Loc.T("about.shortcuts.value")),
                (Loc.T("about.copyright"), "© 2025 OBD2 System. All rights reserved."),
            };

            foreach ((string label, string value) in rows)
            {
                RectangleF row = NextRow(bounds, 50);
                if (row.Bottom > bounds.Bottom - 66)
                {
                    break;
                }

                Draw.TextIn(g, label, Draw.Font(20), Theme.TextSoft,
                    new RectangleF(row.X + 4, row.Y, row.Width * 0.3f, row.Height), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, value, Draw.Font(20), Theme.Text,
                    new RectangleF(row.X + row.Width * 0.3f, row.Y, row.Width * 0.7f - 4, row.Height), StringAlignment.Near, StringAlignment.Center, false);

                using var pen = new Pen(Theme.Border, 1f);
                g.DrawLine(pen, row.X, row.Bottom, row.Right, row.Bottom);
            }

            var exit = new RectangleF(bounds.X, bounds.Bottom - 60, 260, 56);
            DrawGhostButton(g, exit, Loc.T("about.exit"), Theme.Critical, () => OpenModal(
                Loc.T("about.exit.title"),
                Loc.T("about.exit.body"),
                Loc.T("about.exit.ok"), Theme.Critical, () => FindForm()?.Close()), "about-exit", 13f);
        }
    }
}

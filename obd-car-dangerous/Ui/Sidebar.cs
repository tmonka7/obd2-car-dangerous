using System.Drawing.Drawing2D;
using obd_car_dangerous.Services;

namespace obd_car_dangerous.Ui
{
    internal sealed record NavItem(string Key, string LabelKey, string Icon);

    /// <summary>Left navigation rail. Collapses to icons when the window gets narrow.</summary>
    internal sealed class Sidebar : PageBase
    {
        public const float ExpandedWidth = 250f;
        public const float CollapsedWidth = 92f;

        public static readonly NavItem[] Items =
        {
            new("home", "nav.home", "home"),
            new("diagnostics", "nav.diagnostics", "diagnostics"),
            new("livedata", "nav.livedata", "chart"),
            new("dtc", "nav.dtc", "alert"),
            new("dictionary", "nav.dictionary", "book"),
            new("fuel", "nav.fuel", "fuel"),
            new("trip", "nav.trip", "pin"),
            new("alarms", "nav.alarms", "history"),
            new("settings", "nav.settings", "settings"),
        };

        public string Selected { get; set; } = "home";

        public bool Collapsed { get; set; }

        public Action<string>? ItemSelected { get; set; }

        public Action? ToggleRequested { get; set; }

        protected override void Render(Graphics g)
        {
            using (var brush = new LinearGradientBrush(new RectangleF(0, 0, Math.Max(1, W), H + 1),
                       Theme.ShellTop, Theme.ShellBottom, LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, 0, 0, W, H);
            }

            bool compact = Collapsed;
            float pad = compact ? 16 : 18;

            // Hamburger / logo row.
            var toggle = new RectangleF(pad, 22, 46, 46);
            Icons.Draw(g, compact ? "menu" : "close", toggle, Hovered(Draw.Alpha(Color.White, 220), "nav-toggle"), Theme.ShellTop);
            Hit(new RectangleF(pad - 6, 16, 58, 58), () => ToggleRequested?.Invoke(), "nav-toggle");

            if (!compact)
            {
                Draw.TextIn(g, Services.Loc.T("nav.menu"), Draw.Font(19, FontStyle.Bold), Draw.Alpha(Color.White, 150),
                    new RectangleF(pad + 58, 22, W - pad - 70, 46), StringAlignment.Near, StringAlignment.Center, false);
            }

            float y = 96;
            float itemH = 56;
            float gap = 6;

            foreach (NavItem item in Items)
            {
                bool active = item.Key == Selected;
                var rect = new RectangleF(compact ? 10 : 14, y, W - (compact ? 20 : 28), itemH);
                string id = $"nav-{item.Key}";

                if (active)
                {
                    Draw.FillRounded(g, Theme.Accent, rect, 14f);
                }
                else if (IsHover(id) || IsPressed(id))
                {
                    Draw.FillRounded(g, Draw.Alpha(Color.White, IsPressed(id) ? 46 : 28), rect, 14f);
                }

                Color knockout = active ? Theme.Accent : Theme.ShellTop;
                var iconBox = new RectangleF(rect.X + (compact ? (rect.Width - 30) / 2f : 18), rect.Y + 13, 30, 30);
                Icons.Draw(g, item.Icon, iconBox, active ? Color.White : Theme.ShellText, knockout);

                if (!compact)
                {
                    Draw.TextIn(g, Services.Loc.T(item.LabelKey), Draw.Font(21, active ? FontStyle.Bold : FontStyle.Regular),
                        active ? Color.White : Theme.ShellText,
                        new RectangleF(rect.X + 62, rect.Y, rect.Width - 72, rect.Height),
                        StringAlignment.Near, StringAlignment.Center, false);
                }

                if (item.Key == "dtc")
                {
                    int count = AppState.Dtc.Count(DtcStatus.Current);
                    if (count > 0)
                    {
                        var badge = compact
                            ? new RectangleF(rect.Right - 28, rect.Y + 6, 24, 24)
                            : new RectangleF(rect.Right - 44, rect.Y + 16, 30, 24);
                        Draw.Pill(g, badge, Theme.Critical, count.ToString(), Draw.Font(16, FontStyle.Bold), Color.White);
                    }
                }

                string key = item.Key;
                Hit(rect, () => ItemSelected?.Invoke(key), id);
                y += itemH + gap;
            }

            DrawFooter(g, compact);
        }

        private void DrawFooter(Graphics g, bool compact)
        {
            var box = new RectangleF(compact ? 10 : 14, H - 118, W - (compact ? 20 : 28), 96);
            Draw.FillRounded(g, Draw.Alpha(Color.White, 22), box, 14f);

            bool live = AppState.Connection.IsLive;
            bool online = AppState.Connection.IsConnected;
            Color dot = live ? Theme.Good : online ? Theme.Warn : Theme.Critical;
            g.FillEllipse(new SolidBrush(dot), box.X + (compact ? box.Width / 2f - 7 : 18), box.Y + 20, 14, 14);

            if (compact)
            {
                Draw.TextIn(g, online ? "ON" : "OFF", Draw.Font(15, FontStyle.Bold), Draw.Alpha(Color.White, 210),
                    new RectangleF(box.X, box.Y + 44, box.Width, 22), StringAlignment.Center, StringAlignment.Center, false);
                Draw.TextIn(g, $"{AppState.HealthScore}", Draw.Font(19, FontStyle.Bold), Color.White,
                    new RectangleF(box.X, box.Y + 66, box.Width, 24), StringAlignment.Center, StringAlignment.Center, false);
                return;
            }

            Draw.Text(g, AppState.Connection.StatusText, Draw.Font(19, FontStyle.Bold), Color.White, box.X + 40, box.Y + 14);
            Draw.Text(g, $"{AppState.Connection.Current.Name} · {AppState.Connection.Current.Transport}",
                Draw.Font(16), Draw.Alpha(Color.White, 170), box.X + 18, box.Y + 44);
            Draw.Text(g, Loc.T("common.healthline", AppState.HealthScore, AppState.HealthLabel),
                Draw.Font(16), Draw.Alpha(Color.White, 170), box.X + 18, box.Y + 66);
        }
    }
}

using System.Drawing.Drawing2D;
using obd_car_dangerous.Services;

namespace obd_car_dangerous.Ui
{
    internal sealed record NavItem(string Key, string LabelKey, string Icon);

    /// <summary>Left navigation rail. Collapses to icons when the window gets narrow.</summary>
    internal sealed class Sidebar : PageBase
    {
        public const float ExpandedWidth = 226f;
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
            // The top cell is the left end of the top bar, the same colour and hairline as the pages' bar.
            const float top = PageBase.TopBarHeight;
            using (var bar = new SolidBrush(Theme.ShellTop))
            {
                g.FillRectangle(bar, 0, 0, W, top);
            }

            using (var brush = new LinearGradientBrush(new RectangleF(0, top, Math.Max(1, W), H - top + 1),
                       Theme.ShellTop, Theme.ShellBottom, LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, 0, top, W, H - top);
            }

            using (var edge = new Pen(Theme.ShellLine, 1f))
            {
                g.DrawLine(edge, 0, top - 0.5f, W, top - 0.5f);
                g.DrawLine(edge, W - 0.5f, 10, W - 0.5f, top - 10);
            }

            bool compact = Collapsed;

            // The Redline logo, cut from the design; clicking it folds the rail to icons and back.
            Bitmap logo = Pages.Scan.ScanKit.Sprite(compact ? "logo-mark" : "logo");
            float logoH = compact ? 36f : 42f;
            float logoW = Math.Min(W - 28, logo.Width * logoH / logo.Height);
            logoH = logo.Height * logoW / logo.Width;
            var logoBox = new RectangleF((W - logoW) / 2f, (top - logoH) / 2f, logoW, logoH);
            g.DrawImage(logo, logoBox);
            if (IsHover("nav-toggle"))
            {
                Draw.FillRounded(g, Color.FromArgb(14, 255, 255, 255), RectangleF.Inflate(logoBox, 8, 6), 10f);
            }

            Hit(RectangleF.Inflate(logoBox, 8, 8), () => ToggleRequested?.Invoke(), "nav-toggle");

            float y = top + 22;
            float itemH = 62;

            foreach (NavItem item in Items)
            {
                bool active = item.Key == Selected;
                var rect = new RectangleF(0, y, W - (compact ? 8 : 14), itemH - 4);
                string id = $"nav-{item.Key}";

                if (active)
                {
                    DrawActive(g, rect);
                }
                else if (IsHover(id) || IsPressed(id))
                {
                    Draw.FillRounded(g, Color.FromArgb(IsPressed(id) ? 30 : 16, 255, 255, 255), RectangleF.FromLTRB(8, rect.Y, rect.Right, rect.Bottom), 10f);
                }

                var iconBox = new RectangleF(compact ? (W - 30) / 2f : 24, rect.Y + (rect.Height - 30) / 2f, 30, 30);
                Icons.Draw(g, item.Icon, iconBox, active ? Color.White : Color.FromArgb(226, 233, 243), active ? Color.FromArgb(190, 14, 38) : Theme.ShellTop);

                if (!compact)
                {
                    Draw.TextIn(g, Services.Loc.T(item.LabelKey), Draw.Font(19, active ? FontStyle.Bold : FontStyle.Regular),
                        active ? Color.White : Theme.ShellText,
                        new RectangleF(70, rect.Y, rect.Width - 76, rect.Height),
                        StringAlignment.Near, StringAlignment.Center, false);
                }

                if (item.Key == "dtc")
                {
                    int count = AppState.Dtc.Count(DtcStatus.Current);
                    if (count > 0)
                    {
                        var badge = compact
                            ? new RectangleF(rect.Right - 30, rect.Y + 6, 24, 22)
                            : new RectangleF(rect.Right - 32, rect.Y + (rect.Height - 22) / 2f, 26, 22);
                        Draw.GlowFill(g, badge, Theme.Accent, 11f);
                        Draw.TextCentered(g, count.ToString(), Draw.Font(15, FontStyle.Bold), Color.White, badge);
                    }
                }

                // Hairline between rows, as in the design.
                if (!active && item != Items[^1])
                {
                    using var line = new Pen(Color.FromArgb(18, 28, 40), 1f);
                    g.DrawLine(line, compact ? 12 : 22, rect.Bottom + 2, rect.Right - 4, rect.Bottom + 2);
                }

                string key = item.Key;
                Hit(rect, () => ItemSelected?.Invoke(key), id);
                y += itemH;
            }
        }

        /// <summary>Selected row: red glass running from a bright edge on the left into the rail.</summary>
        private static void DrawActive(Graphics g, RectangleF rect)
        {
            for (int i = 3; i >= 1; i--)
            {
                Draw.FillRounded(g, Color.FromArgb(14, 255, 20, 50), RectangleF.FromLTRB(-20, rect.Y - i * 2, rect.Right + i * 2, rect.Bottom + i * 2), 12f + i);
            }

            var body = RectangleF.FromLTRB(-20, rect.Y, rect.Right, rect.Bottom);
            using (var brush = new LinearGradientBrush(new RectangleF(0, rect.Y, rect.Right, rect.Height),
                       Color.FromArgb(232, 22, 48), Color.FromArgb(92, 8, 22), LinearGradientMode.Horizontal))
            {
                Draw.FillRounded(g, brush, body, 12f);
            }

            using (var sheen = new LinearGradientBrush(new RectangleF(0, rect.Y, rect.Right, rect.Height),
                       Color.FromArgb(40, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
            {
                Draw.FillRounded(g, sheen, RectangleF.FromLTRB(-20, rect.Y, rect.Right, rect.Y + rect.Height / 2f), 12f);
            }

            using var edge = new SolidBrush(Color.FromArgb(255, 60, 86));
            g.FillRectangle(edge, 0, rect.Y, 4, rect.Height);
        }
    }
}

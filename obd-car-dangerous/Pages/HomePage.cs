using System.Drawing.Drawing2D;
using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Dashboard: connection state, overall vehicle status and the four quick actions.</summary>
    internal sealed class HomePage : PageBase
    {
        private sealed record Tile(string Key, string LabelKey, string Icon, Color Color, string Page);

        private static readonly Tile[] Tiles =
        {
            new("diag", "nav.diagnostics", "diagnostics", Theme.Orange, "diagnostics"),
            new("live", "nav.livedata", "chart", Color.FromArgb(28, 154, 244), "livedata"),
            new("dtc", "nav.dtc", "alert", Theme.Critical, "dtc"),
            new("set", "nav.settings", "settings", Theme.Violet, "settings"),
        };

        public override string Title => Loc.T("app.title");

        protected override void Render(Graphics g)
        {
            const float pad = 28f;

            DrawHero(g, pad);

            var status = new RectangleF(pad, 146, W - pad * 2, 212);
            DrawStatusCard(g, status);

            float tileTop = status.Bottom + 22;
            DrawQuickTiles(g, new RectangleF(pad, tileTop, W - pad * 2, 198));

            DrawLiveStrip(g, new RectangleF(pad, tileTop + 218, W - pad * 2, H - tileTop - 218 - pad));
        }

        private void DrawHero(Graphics g, float pad)
        {
            Draw.Text(g, Title, Draw.Font(36, FontStyle.Bold), Theme.Text, pad, 26);

            bool live = AppState.Connection.IsLive;
            Color color = live ? Theme.Good : AppState.Connection.IsDemo ? Theme.Warn : Theme.TextSoft;
            var chip = new RectangleF(pad, 80, 210, 40);
            Draw.FillRounded(g, Draw.Alpha(color, 38), chip, 20f);
            g.FillEllipse(new SolidBrush(color), chip.X + 14, chip.Y + 13, 14, 14);
            Draw.TextIn(g, AppState.Connection.StatusText, Draw.Font(20, FontStyle.Bold), color,
                new RectangleF(chip.X + 38, chip.Y, chip.Width - 46, chip.Height), StringAlignment.Near, StringAlignment.Center, false);
            Hit(chip, () => Shell.Navigate("settings", "connection"), "home-chip");

            var scan = new RectangleF(chip.Right + 14, 80, 196, 40);
            Draw.TextIn(g, Loc.T("home.lastscan", AppState.LastScan.ToString("HH:mm")), Draw.Font(18), Theme.TextSoft, scan,
                StringAlignment.Near, StringAlignment.Center, false);

            DrawCar(g, new RectangleF(W - 430, 6, 400, 132));

            // Clock cluster, mirroring the header used on the other screens.
            string clock = DateTime.Now.ToString("HH:mm");
            Draw.TextIn(g, clock, Draw.Font(19, FontStyle.Bold), Theme.TextSoft,
                new RectangleF(W - 150, 12, 120, 28), StringAlignment.Far, StringAlignment.Center, false);
        }

        private static void DrawCar(Graphics g, RectangleF box)
        {
            float w = box.Width;
            float h = box.Height;
            float x = box.X;
            float y = box.Y;

            using var glow = new LinearGradientBrush(
                new RectangleF(x, y, w, h), Draw.Alpha(Theme.Accent, 28), Draw.Alpha(Theme.Accent, 0), LinearGradientMode.Vertical);
            g.FillEllipse(glow, x, y + h * 0.35f, w, h * 0.6f);

            Color body = Theme.Dark ? Color.FromArgb(206, 222, 244) : Color.FromArgb(232, 240, 250);
            Color shade = Theme.Dark ? Color.FromArgb(150, 176, 210) : Color.FromArgb(186, 202, 224);

            using var bodyBrush = new SolidBrush(body);
            using var shadeBrush = new SolidBrush(shade);
            using var glassBrush = new SolidBrush(Color.FromArgb(120, 160, 205));
            using var tyre = new SolidBrush(Color.FromArgb(28, 40, 56));

            g.FillPolygon(bodyBrush, new[]
            {
                new PointF(x + w * 0.06f, y + h * 0.74f),
                new PointF(x + w * 0.14f, y + h * 0.5f),
                new PointF(x + w * 0.32f, y + h * 0.3f),
                new PointF(x + w * 0.64f, y + h * 0.27f),
                new PointF(x + w * 0.84f, y + h * 0.46f),
                new PointF(x + w * 0.96f, y + h * 0.6f),
                new PointF(x + w * 0.96f, y + h * 0.74f),
            });
            g.FillPolygon(glassBrush, new[]
            {
                new PointF(x + w * 0.24f, y + h * 0.48f),
                new PointF(x + w * 0.37f, y + h * 0.33f),
                new PointF(x + w * 0.6f, y + h * 0.31f),
                new PointF(x + w * 0.7f, y + h * 0.48f),
            });
            g.FillRectangle(shadeBrush, x + w * 0.06f, y + h * 0.72f, w * 0.9f, h * 0.04f);

            foreach (float cx in new[] { 0.26f, 0.76f })
            {
                g.FillEllipse(tyre, x + w * cx - h * 0.16f, y + h * 0.6f, h * 0.32f, h * 0.32f);
                g.FillEllipse(bodyBrush, x + w * cx - h * 0.08f, y + h * 0.68f, h * 0.16f, h * 0.16f);
            }
        }

        private void DrawStatusCard(Graphics g, RectangleF bounds)
        {
            int current = AppState.Dtc.Count(DtcStatus.Current);
            bool critical = AppState.Dtc.HasCritical;
            Color from = critical ? Color.FromArgb(240, 68, 86) : current > 0 ? Color.FromArgb(250, 190, 40) : Color.FromArgb(46, 204, 113);
            Color to = critical ? Color.FromArgb(198, 20, 46) : current > 0 ? Color.FromArgb(236, 150, 20) : Color.FromArgb(22, 160, 95);

            string headline = critical ? Loc.T("home.danger") : current > 0 ? Loc.T("home.attention") : Loc.T("home.normal");
            string detail = current == 0
                ? Loc.T("home.nofault")
                : Loc.T("home.faults", current);

            Draw.CardShadow(g, bounds, 22f);
            Draw.GradientRounded(g, from, to, bounds, 22f, LinearGradientMode.Horizontal);

            float iconSize = 132;
            var iconBox = new RectangleF(bounds.X + 54, bounds.Y + (bounds.Height - iconSize) / 2f, iconSize, iconSize);
            using (var ring = new Pen(Color.White, 7f))
            {
                g.DrawEllipse(ring, iconBox);
            }

            if (current == 0)
            {
                Draw.CheckMark(g, iconBox, Color.White, 10f);
            }
            else
            {
                Draw.WarningTriangle(g,
                    new RectangleF(iconBox.X + 26, iconBox.Y + 30, iconSize - 52, iconSize - 62), Color.White, from);
            }

            float textX = iconBox.Right + 46;
            Draw.Text(g, Loc.T("home.status"), Draw.Font(28, FontStyle.Regular), Draw.Alpha(Color.White, 225), textX, bounds.Y + 36);
            Draw.Text(g, headline, Draw.Font(64, FontStyle.Bold), Color.White, textX, bounds.Y + 70);
            Draw.Text(g, detail, Draw.Font(22), Draw.Alpha(Color.White, 225), textX, bounds.Y + 152);

            // Right hand summary: health score.
            var ringBox = new RectangleF(bounds.Right - 190, bounds.Y + 36, 140, 140);
            Draw.RingGauge(g, ringBox, AppState.HealthScore / 100f, Draw.Alpha(Color.White, 210), Color.White, 12f);
            Draw.TextCentered(g, AppState.HealthScore.ToString(), Draw.Font(42, FontStyle.Bold), Color.White,
                new RectangleF(ringBox.X, ringBox.Y + 34, ringBox.Width, 48));
            Draw.TextCentered(g, Loc.T("common.health"), Draw.Font(16, FontStyle.Bold), Draw.Alpha(Color.White, 215),
                new RectangleF(ringBox.X, ringBox.Y + 84, ringBox.Width, 24));

            Hit(bounds, () => Shell.Navigate(current > 0 ? "dtc" : "diagnostics"), "home-status");
        }

        private void DrawQuickTiles(Graphics g, RectangleF bounds)
        {
            float gap = 20f;
            float tileW = (bounds.Width - gap * (Tiles.Length - 1)) / Tiles.Length;

            for (int i = 0; i < Tiles.Length; i++)
            {
                Tile tile = Tiles[i];
                var rect = new RectangleF(bounds.X + i * (tileW + gap), bounds.Y, tileW, bounds.Height);
                string id = $"tile-{tile.Key}";

                Draw.Card(g, rect, 20f);

                float size = Math.Min(118f, rect.Width * 0.44f);
                var iconRect = new RectangleF(rect.X + (rect.Width - size) / 2f, rect.Y + 26, size, size);
                Color fill = Hovered(tile.Color, id);
                Draw.FillRounded(g, fill, iconRect, size * 0.24f);
                Icons.Draw(g, tile.Icon, RectangleF.Inflate(iconRect, -size * 0.26f, -size * 0.26f), Color.White, fill);

                Draw.TextIn(g, Loc.T(tile.LabelKey), Draw.Font(23, FontStyle.Bold), Theme.Text,
                    new RectangleF(rect.X, iconRect.Bottom + 10, rect.Width, 40), StringAlignment.Center, StringAlignment.Center, false);

                if (tile.Key == "dtc")
                {
                    int count = AppState.Dtc.Count(DtcStatus.Current);
                    if (count > 0)
                    {
                        var badge = new RectangleF(rect.Right - 52, rect.Y + 16, 36, 30);
                        Draw.Pill(g, badge, Theme.Critical, count.ToString(), Draw.Font(18, FontStyle.Bold), Color.White);
                    }
                }

                Hit(rect, () => Shell.Navigate(tile.Page), id);
            }
        }

        private void DrawLiveStrip(Graphics g, RectangleF bounds)
        {
            if (bounds.Height < 90)
            {
                return;
            }

            (string Label, string Value, string Unit, float Fraction, Color Color, string Page)[] items =
            {
                (Loc.Pid("rpm", "Engine RPM"), $"{AppState.Telemetry.Rpm:0}", "rpm", AppState.Telemetry.Rpm / 6000f, Theme.Accent, "livedata"),
                (Loc.Pid("speed", "Vehicle Speed"), $"{AppState.Settings.Speed(AppState.Telemetry.Speed):0}", AppState.Settings.SpeedUnit,
                    AppState.Telemetry.Speed / 220f, Theme.Good, "livedata"),
                (Loc.Pid("coolant", "Coolant Temp"), $"{AppState.Settings.Temperature(AppState.Telemetry.CoolantTemp):0}", AppState.Settings.TempUnit,
                    AppState.Telemetry.CoolantTemp / 130f, Theme.Orange, "livedata"),
                (Loc.Pid("fuellevel", "Fuel Level"), $"{AppState.Telemetry.FuelLevel:0}", "%", AppState.Telemetry.FuelLevel / 100f, Theme.Violet, "fuel"),
                (Loc.T("home.trip"), $"{AppState.Settings.Distance(AppState.Telemetry.DistanceKm):0.0}", AppState.Settings.DistanceUnit,
                    Math.Min(1f, AppState.Telemetry.DistanceKm / 200f), Color.FromArgb(0, 176, 185), "trip"),
            };

            float gap = 18f;
            float cardW = (bounds.Width - gap * (items.Length - 1)) / items.Length;

            for (int i = 0; i < items.Length; i++)
            {
                var rect = new RectangleF(bounds.X + i * (cardW + gap), bounds.Y, cardW, bounds.Height);
                string id = $"strip-{i}";
                Draw.Card(g, rect, 18f, IsHover(id) ? Theme.CardAlt : Theme.Card);

                Draw.TextIn(g, items[i].Label, Draw.Font(18, FontStyle.Bold), Theme.TextSoft,
                    new RectangleF(rect.X + 18, rect.Y + 14, rect.Width - 36, 26), StringAlignment.Near, StringAlignment.Center, false);

                Draw.Text(g, items[i].Value, Draw.Font(40, FontStyle.Bold), Theme.Text, rect.X + 18, rect.Y + 44);
                SizeF size = Draw.Measure(g, items[i].Value, Draw.Font(40, FontStyle.Bold));
                Draw.Text(g, items[i].Unit, Draw.Font(18), Theme.TextSoft, rect.X + 22 + size.Width, rect.Y + 68);

                var bar = new RectangleF(rect.X + 18, rect.Bottom - 26, rect.Width - 36, 10);
                Draw.FillRounded(g, Theme.Dark ? Color.FromArgb(34, 62, 98) : Color.FromArgb(232, 238, 246), bar, 5f);
                Draw.FillRounded(g, items[i].Color,
                    new RectangleF(bar.X, bar.Y, Math.Max(10f, bar.Width * Math.Clamp(items[i].Fraction, 0f, 1f)), bar.Height), 5f);

                string page = items[i].Page;
                Hit(rect, () => Shell.Navigate(page), id);
            }
        }
    }
}

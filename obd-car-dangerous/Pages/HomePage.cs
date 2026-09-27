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
            new("live", "nav.livedata", "chart", Theme.Good, "livedata"),
            new("dtc", "nav.dtc", "alert", Theme.Critical, "dtc"),
            new("set", "nav.settings", "settings", Theme.Info, "settings"),
        };

        public override string Title => Loc.T("app.title");

        protected override void Render(Graphics g)
        {
            const float pad = 28f;

            float top = DrawHeader(g, Loc.T("home.lastscan", AppState.LastScan.ToString("HH:mm")));

            var status = new RectangleF(pad, top + 22, W - pad * 2, 222);
            DrawStatusCard(g, status);

            float tileTop = status.Bottom + 22;
            DrawQuickTiles(g, new RectangleF(pad, tileTop, W - pad * 2, 198));

            DrawLiveStrip(g, new RectangleF(pad, tileTop + 218, W - pad * 2, H - tileTop - 218 - pad));
        }

        private void DrawStatusCard(Graphics g, RectangleF bounds)
        {
            int current = AppState.Dtc.Count(DtcStatus.Current);
            bool critical = AppState.Dtc.HasCritical;
            Color from = critical ? Theme.Critical : current > 0 ? Theme.Warn : Theme.Good;
            Color to = critical ? Theme.AccentDeep : current > 0 ? Color.FromArgb(200, 110, 10) : Color.FromArgb(10, 130, 84);

            string headline = critical ? Loc.T("home.danger") : current > 0 ? Loc.T("home.attention") : Loc.T("home.normal");
            string detail = current == 0
                ? Loc.T("home.nofault")
                : Loc.T("home.faults", current);

            // Dark panel lit from the left in the status colour, with the car of the scan design behind it.
            Draw.Card(g, bounds, 14f);
            GraphicsState clip = g.Save();
            using (GraphicsPath shape = Draw.RoundedPath(bounds, 14f))
            {
                g.SetClip(shape);
            }

            var washArea = RectangleF.FromLTRB(bounds.X, bounds.Y, bounds.X + bounds.Width * 0.6f, bounds.Bottom);
            using (var wash = new LinearGradientBrush(RectangleF.Inflate(washArea, 1, 1), Draw.Alpha(from, 70), Draw.Alpha(to, 0), LinearGradientMode.Horizontal))
            {
                g.FillRectangle(wash, washArea);
            }

            // The car sits between the text and the health ring, and is left out when there is no room.
            float textRight = bounds.X + 232 + Math.Max(
                Draw.Measure(g, headline, Draw.Font(64, FontStyle.Bold)).Width,
                Draw.Measure(g, detail, Draw.Font(22)).Width);
            Bitmap car = Scan.ScanKit.Sprite("car");
            float room = bounds.Right - 215 - textRight;
            float carW = Math.Min(car.Width * bounds.Height * 1.12f / car.Height, room * 1.15f);
            if (carW > 200)
            {
                float carH = car.Height * carW / car.Width;
                float carX = bounds.Right - 215 - carW * 0.95f;
                g.DrawImage(car, new RectangleF(carX, bounds.Y + (bounds.Height - carH) / 2f, carW, carH));
            }

            g.Restore(clip);
            Draw.StrokeRounded(g, Draw.Alpha(from, 150), bounds, 14f, 1.5f);

            float iconSize = 132;
            var iconBox = new RectangleF(bounds.X + 54, bounds.Y + (bounds.Height - iconSize) / 2f, iconSize, iconSize);
            for (int i = 3; i >= 1; i--)
            {
                using var halo = new Pen(Color.FromArgb(22, from), 7f + i * 5f);
                g.DrawEllipse(halo, iconBox);
            }

            using (var ring = new Pen(from, 7f))
            {
                g.DrawEllipse(ring, iconBox);
            }

            if (current == 0)
            {
                Draw.CheckMark(g, iconBox, from, 10f);
            }
            else
            {
                Draw.WarningTriangle(g,
                    new RectangleF(iconBox.X + 26, iconBox.Y + 30, iconSize - 52, iconSize - 62), from, Theme.Card);
            }

            float textX = iconBox.Right + 46;
            Draw.Text(g, Loc.T("home.status"), Draw.Font(26), Theme.TextSoft, textX, bounds.Y + 36);
            Draw.Text(g, headline, Draw.Font(64, FontStyle.Bold), Color.White, textX, bounds.Y + 68);
            Draw.Text(g, detail, Draw.Font(22), from, textX, bounds.Y + 152);

            // Right hand summary: health score.
            var ringBox = new RectangleF(bounds.Right - 190, bounds.Y + 36, 140, 140);
            Draw.RingGauge(g, ringBox, AppState.HealthScore / 100f, to, from, 12f);
            Draw.TextCentered(g, AppState.HealthScore.ToString(), Draw.Font(42, FontStyle.Bold), Color.White,
                new RectangleF(ringBox.X, ringBox.Y + 34, ringBox.Width, 48));
            Draw.TextCentered(g, Loc.T("common.health"), Draw.Font(16, FontStyle.Bold), Theme.TextSoft,
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

                Draw.Card(g, rect, 14f, IsHover(id) ? Theme.CardAlt : Theme.Card);

                // Icon tile in the manner of the scan design's module tiles: tinted glass with a glow.
                float size = Math.Min(118f, rect.Width * 0.44f);
                var iconRect = new RectangleF(rect.X + (rect.Width - size) / 2f, rect.Y + 26, size, size);
                for (int glow = 3; glow >= 1; glow--)
                {
                    Draw.FillRounded(g, Color.FromArgb(14, tile.Color), RectangleF.Inflate(iconRect, glow * 3, glow * 3), size * 0.22f + glow * 3);
                }

                using (var glass = new LinearGradientBrush(iconRect, Draw.Lerp(tile.Color, Theme.Card, 0.55f), Draw.Lerp(tile.Color, Theme.Card, 0.82f), LinearGradientMode.Vertical))
                {
                    Draw.FillRounded(g, glass, iconRect, size * 0.22f);
                }

                Draw.StrokeRounded(g, Draw.Alpha(tile.Color, 170), iconRect, size * 0.22f, 1.5f);
                Icons.Draw(g, tile.Icon, RectangleF.Inflate(iconRect, -size * 0.26f, -size * 0.26f),
                    Draw.Lerp(tile.Color, Color.White, 0.35f), Draw.Lerp(tile.Color, Theme.Card, 0.7f));

                Draw.TextIn(g, Loc.T(tile.LabelKey), Draw.Font(23, FontStyle.Bold), Theme.Text,
                    new RectangleF(rect.X, iconRect.Bottom + 10, rect.Width, 40), StringAlignment.Center, StringAlignment.Center, false);

                if (tile.Key == "dtc")
                {
                    int count = AppState.Dtc.Count(DtcStatus.Current);
                    if (count > 0)
                    {
                        var badge = new RectangleF(rect.Right - 52, rect.Y + 16, 36, 30);
                        Draw.GlowFill(g, badge, Theme.Accent, 14f);
                        Draw.TextCentered(g, count.ToString(), Draw.Font(18, FontStyle.Bold), Color.White, badge);
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
                (Loc.Pid("fuellevel", "Fuel Level"), $"{AppState.Telemetry.FuelLevel:0}", "%", AppState.Telemetry.FuelLevel / 100f, Theme.Info, "fuel"),
                (Loc.T("home.trip"), $"{AppState.Settings.Distance(AppState.Telemetry.DistanceKm):0.0}", AppState.Settings.DistanceUnit,
                    Math.Min(1f, AppState.Telemetry.DistanceKm / 200f), Theme.Violet, "trip"),
            };

            float gap = 18f;
            float cardW = (bounds.Width - gap * (items.Length - 1)) / items.Length;

            for (int i = 0; i < items.Length; i++)
            {
                var rect = new RectangleF(bounds.X + i * (cardW + gap), bounds.Y, cardW, bounds.Height);
                string id = $"strip-{i}";
                Draw.Card(g, rect, 14f, IsHover(id) ? Theme.CardAlt : Theme.Card);

                Draw.TextIn(g, items[i].Label, Draw.Font(18, FontStyle.Bold), Theme.TextSoft,
                    new RectangleF(rect.X + 18, rect.Y + 14, rect.Width - 36, 26), StringAlignment.Near, StringAlignment.Center, false);

                Draw.Text(g, items[i].Value, Draw.Font(40, FontStyle.Bold), Theme.Text, rect.X + 18, rect.Y + 44);
                SizeF size = Draw.Measure(g, items[i].Value, Draw.Font(40, FontStyle.Bold));
                Draw.Text(g, items[i].Unit, Draw.Font(18), Theme.TextSoft, rect.X + 22 + size.Width, rect.Y + 68);

                var bar = new RectangleF(rect.X + 18, rect.Bottom - 26, rect.Width - 36, 8);
                Draw.FillRounded(g, Theme.Track, bar, 4f);
                var fill = new RectangleF(bar.X, bar.Y, Math.Max(8f, bar.Width * Math.Clamp(items[i].Fraction, 0f, 1f)), bar.Height);
                Draw.FillRounded(g, Draw.Alpha(items[i].Color, 40), RectangleF.Inflate(fill, 3, 3), 7f);
                Draw.GradientRounded(g, Draw.Lerp(items[i].Color, Color.Black, 0.45f), items[i].Color, fill, 4f, LinearGradientMode.Horizontal);

                string page = items[i].Page;
                Hit(rect, () => Shell.Navigate(page), id);
            }
        }
    }
}

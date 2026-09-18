using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Fuel economy: rolling average, instant rate and what this trip has used.</summary>
    internal sealed class FuelPage : PageBase
    {
        private static readonly string[] TabNames = { "Average", "Instant", "Trip" };
        private int tab;

        public override string Title => "Fuel Consumption";

        public override bool ShowBack => true;

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);

            var tabsRect = new RectangleF(pad, top + 18, W - pad * 2, 58);
            DrawTabs(g, tabsRect, TabNames, tab, index =>
            {
                tab = index;
                Invalidate();
            }, "fuel-tab");

            float cardsTop = tabsRect.Bottom + 20;
            var cards = new RectangleF(pad, cardsTop, W - pad * 2, 168);
            DrawSummary(g, cards);

            var chart = new RectangleF(pad, cards.Bottom + 20, W - pad * 2, H - cards.Bottom - 20 - pad);
            Draw.Card(g, chart, 18f);
            DrawChart(g, RectangleF.Inflate(chart, -10, -10));
        }

        private void DrawSummary(Graphics g, RectangleF bounds)
        {
            Telemetry t = AppState.Telemetry;
            float gap = 20f;
            float half = (bounds.Width - gap) / 2f;

            var left = new RectangleF(bounds.X, bounds.Y, half, bounds.Height);
            var right = new RectangleF(bounds.X + half + gap, bounds.Y, half, bounds.Height);

            (string Label, string Value, string Unit) primary = tab switch
            {
                0 => ("Average Economy", t.AverageConsumption.ToString("0.0"), AppState.Settings.ConsumptionUnit),
                1 => ("Instant Economy", t.Speed < 3 ? "--" : t.InstantConsumption.ToString("0.0"), AppState.Settings.ConsumptionUnit),
                _ => ("Fuel Used (trip)", (t.DistanceKm * t.AverageConsumption / 100f).ToString("0.00"), "L"),
            };

            Draw.Card(g, left, 18f);
            var pumpBox = new RectangleF(left.X + 26, left.Y + (left.Height - 76) / 2f, 76, 76);
            Icons.Draw(g, "fuel", pumpBox, Theme.Accent, Theme.Card);

            Draw.Text(g, primary.Label, Draw.Font(21), Theme.TextSoft, pumpBox.Right + 24, left.Y + 30);
            Draw.Text(g, primary.Value, Draw.Font(58, FontStyle.Bold), Theme.Text, pumpBox.Right + 24, left.Y + 58);
            SizeF size = Draw.Measure(g, primary.Value, Draw.Font(58, FontStyle.Bold));
            Draw.Text(g, primary.Unit, Draw.Font(20), Theme.TextSoft, pumpBox.Right + 30 + size.Width, left.Y + 96);

            // Fuel level card.
            Draw.Card(g, right, 18f);
            var levelIcon = new RectangleF(right.X + 26, right.Y + (right.Height - 76) / 2f, 76, 76);
            Color levelColor = t.FuelLevel < 15 ? Theme.Critical : t.FuelLevel < 30 ? Theme.Warn : Theme.Good;
            Icons.Draw(g, "fuel", levelIcon, levelColor, Theme.Card);

            Draw.Text(g, "Fuel Level", Draw.Font(21), Theme.TextSoft, levelIcon.Right + 24, right.Y + 26);
            Draw.Text(g, $"{t.FuelLevel:0}%", Draw.Font(52, FontStyle.Bold), Theme.Text, levelIcon.Right + 24, right.Y + 52);

            var bar = new RectangleF(levelIcon.Right + 24, right.Bottom - 46, right.Width - (levelIcon.Right - right.X) - 56, 18);
            Draw.FillRounded(g, Theme.Dark ? Color.FromArgb(34, 62, 98) : Color.FromArgb(230, 237, 245), bar, 9f);
            Draw.FillRounded(g, levelColor, new RectangleF(bar.X, bar.Y, Math.Max(12f, bar.Width * t.FuelLevel / 100f), bar.Height), 9f);

            float range = t.AverageConsumption > 0.5f ? t.FuelLevel / 100f * 50f / t.AverageConsumption * 100f : 0;
            Draw.TextIn(g, $"Range approx. {AppState.Settings.Distance(range):0} {AppState.Settings.DistanceUnit}",
                Draw.Font(17), Theme.TextSoft,
                new RectangleF(right.Right - 300, right.Y + 24, 270, 26), StringAlignment.Far, StringAlignment.Center, false);
        }

        private void DrawChart(Graphics g, RectangleF bounds)
        {
            Telemetry t = AppState.Telemetry;

            if (tab == 1)
            {
                float[] values = Charts.Downsample(t.HistoryOf("consumption").Recent(60_000 / Telemetry.TickMs), 120);
                Charts.Line(g, bounds, values, 0, 30, Theme.Accent,
                    $"Instant economy ({AppState.Settings.ConsumptionUnit})",
                    new[] { "60s", "50s", "40s", "30s", "20s", "10s", "now" });
                return;
            }

            if (tab == 2)
            {
                DrawTripBreakdown(g, bounds);
                return;
            }

            const int buckets = 6;
            float[] samples = t.HistoryOf("consumption").Recent(600_000 / Telemetry.TickMs);
            float[] averaged = Charts.Downsample(samples, buckets);
            if (averaged.Length == 0)
            {
                averaged = new float[buckets];
            }

            var labels = new string[averaged.Length];
            for (int i = 0; i < averaged.Length; i++)
            {
                labels[i] = DateTime.Now.AddMinutes(-(averaged.Length - 1 - i) * 10).ToString("HH:mm");
            }

            float max = Math.Max(10f, averaged.Length == 0 ? 20f : averaged.Max() * 1.3f);
            Charts.Bars(g, bounds, averaged, labels, max, Theme.Accent,
                $"Fuel Economy ({AppState.Settings.ConsumptionUnit})", "10 minute buckets");
        }

        private void DrawTripBreakdown(Graphics g, RectangleF bounds)
        {
            Telemetry t = AppState.Telemetry;
            Draw.Text(g, "This trip", Draw.Font(20, FontStyle.Bold), Theme.Text, bounds.X + 12, bounds.Y + 10);

            float used = t.DistanceKm * t.AverageConsumption / 100f;
            (string Label, string Value)[] rows =
            {
                ("Distance", $"{AppState.Settings.Distance(t.DistanceKm):0.0} {AppState.Settings.DistanceUnit}"),
                ("Driving time", TimeSpan.FromSeconds(t.DrivingSeconds).ToString(@"h\:mm")),
                ("Fuel used", $"{used:0.00} L"),
                ("Average economy", $"{t.AverageConsumption:0.0} {AppState.Settings.ConsumptionUnit}"),
                ("Average speed", $"{AppState.Settings.Speed(t.AvgSpeed):0.0} {AppState.Settings.SpeedUnit}"),
                ("Idle fuel rate", $"{Math.Max(0.6f, t.FuelRate * 0.25f):0.0} L/h"),
                ("CO₂ estimate", $"{used * 2.31f:0.0} kg"),
                ("Cost at 1.75/L", $"{used * 1.75f:0.00}"),
            };

            float columnW = bounds.Width / 2f;
            float rowH = Math.Min(64f, (bounds.Height - 60) / 4f);
            for (int i = 0; i < rows.Length; i++)
            {
                int column = i / 4;
                int row = i % 4;
                var rect = new RectangleF(bounds.X + 12 + column * columnW, bounds.Y + 50 + row * rowH, columnW - 24, rowH);

                Draw.TextIn(g, rows[i].Label, Draw.Font(20), Theme.TextSoft,
                    new RectangleF(rect.X, rect.Y, rect.Width * 0.6f, rect.Height), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, rows[i].Value, Draw.Font(22, FontStyle.Bold), Theme.Text,
                    new RectangleF(rect.X + rect.Width * 0.5f, rect.Y, rect.Width * 0.5f, rect.Height), StringAlignment.Far, StringAlignment.Center, false);

                using var pen = new Pen(Theme.Border, 1f);
                g.DrawLine(pen, rect.X, rect.Bottom, rect.Right, rect.Bottom);
            }
        }
    }
}

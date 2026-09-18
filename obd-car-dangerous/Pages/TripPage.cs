using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Trip computer: distance, time and speeds since the last reset.</summary>
    internal sealed class TripPage : PageBase
    {
        public override string Title => Loc.T("trip.title");

        public override bool ShowBack => true;

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);
            Telemetry t = AppState.Telemetry;
            AppSettings settings = AppState.Settings;

            (string Label, string Value, string Unit, string Icon, Color Color)[] cards =
            {
                (Loc.T("trip.distance"), settings.Distance(t.DistanceKm).ToString("0.0"), settings.DistanceUnit, "pin", Theme.Accent),
                (Loc.T("trip.time"), TimeSpan.FromSeconds(t.DrivingSeconds).ToString(@"h\:mm"), "h", "clock", Theme.Accent),
                (Loc.T("trip.avgspeed"), settings.Speed(t.AvgSpeed).ToString("0.0"), settings.SpeedUnit, "speed", Theme.Accent),
                (Loc.T("trip.maxspeed"), settings.Speed(t.MaxSpeed).ToString("0"), settings.SpeedUnit, "speed", Theme.Critical),
            };

            var grid = new RectangleF(pad, top + 18, W - pad * 2, (H - top - 36) * 0.56f);
            float gap = 20f;
            float cellW = (grid.Width - gap) / 2f;
            float cellH = (grid.Height - gap) / 2f;

            for (int i = 0; i < cards.Length; i++)
            {
                var rect = new RectangleF(
                    grid.X + (i % 2) * (cellW + gap),
                    grid.Y + (i / 2) * (cellH + gap),
                    cellW, cellH);

                Draw.Card(g, rect, 20f);

                float iconSize = Math.Min(96f, cellH * 0.56f);
                var iconBox = new RectangleF(rect.X + 34, rect.Y + (rect.Height - iconSize) / 2f, iconSize, iconSize);
                Icons.Draw(g, cards[i].Icon, iconBox, cards[i].Color, Theme.Card);

                Draw.Text(g, cards[i].Label, Draw.Font(22), Theme.TextSoft, iconBox.Right + 30, rect.Y + rect.Height * 0.24f);
                Draw.Text(g, cards[i].Value, Draw.Font(Math.Min(62f, cellH * 0.34f), FontStyle.Bold), Theme.Text,
                    iconBox.Right + 30, rect.Y + rect.Height * 0.38f);
                Draw.Text(g, cards[i].Unit, Draw.Font(20), Theme.TextSoft, iconBox.Right + 30, rect.Bottom - rect.Height * 0.22f);
            }

            var chart = new RectangleF(pad, grid.Bottom + 18, W - pad * 2 - 230, H - grid.Bottom - 18 - pad);
            Draw.Card(g, chart, 18f);
            float[] speeds = Charts.Downsample(t.HistoryOf("speed").Recent(300_000 / Telemetry.TickMs), 140);
            Charts.Line(g, RectangleF.Inflate(chart, -10, -10), speeds, 0, Math.Max(60f, t.MaxSpeed * 1.2f), Theme.Good,
                Loc.T("trip.profile", settings.SpeedUnit), new[] { "5m", "4m", "3m", "2m", "1m", "now" });

            DrawSidePanel(g, new RectangleF(W - pad - 210, grid.Bottom + 18, 210, chart.Height));
            DrawModal(g);
        }

        private void DrawSidePanel(Graphics g, RectangleF bounds)
        {
            Telemetry t = AppState.Telemetry;
            Draw.Card(g, bounds, 18f);

            Draw.TextIn(g, Loc.T("trip.started"), Draw.Font(17), Theme.TextSoft,
                new RectangleF(bounds.X + 18, bounds.Y + 14, bounds.Width - 36, 24), StringAlignment.Near, StringAlignment.Center, false);
            Draw.TextIn(g, DateTime.Now.AddSeconds(-t.DrivingSeconds).ToString("MMM d, HH:mm"), Draw.Font(20, FontStyle.Bold), Theme.Text,
                new RectangleF(bounds.X + 18, bounds.Y + 38, bounds.Width - 36, 30), StringAlignment.Near, StringAlignment.Center, false);

            Draw.TextIn(g, Loc.T("trip.fuelused"), Draw.Font(17), Theme.TextSoft,
                new RectangleF(bounds.X + 18, bounds.Y + 82, bounds.Width - 36, 24), StringAlignment.Near, StringAlignment.Center, false);
            Draw.TextIn(g, $"{t.DistanceKm * t.AverageConsumption / 100f:0.00} L", Draw.Font(20, FontStyle.Bold), Theme.Text,
                new RectangleF(bounds.X + 18, bounds.Y + 106, bounds.Width - 36, 30), StringAlignment.Near, StringAlignment.Center, false);

            var reset = new RectangleF(bounds.X + 16, bounds.Bottom - 70, bounds.Width - 32, 54);
            DrawGhostButton(g, reset, Loc.T("trip.reset"), Theme.Critical, () => OpenModal(
                Loc.T("trip.reset.title"),
                Loc.T("trip.reset.body"),
                Loc.T("trip.reset.ok"),
                Theme.Critical,
                () =>
                {
                    AppState.Telemetry.ResetTrip();
                    Shell.RefreshShell();
                }), "trip-reset", 13f);
        }
    }
}

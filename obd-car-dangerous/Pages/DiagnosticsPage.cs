using System.Drawing.Drawing2D;
using obd_car_dangerous.Pages.Scan;
using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Vehicle health: overall score, per system status and the full system scan.</summary>
    internal sealed class DiagnosticsPage : PageBase
    {
        /// <summary>Progress of the full system scan (its own screen), or -1 when none is running.</summary>
        private static float ScanProgress => ScanSession.Running ? ScanSession.Fraction : -1f;

        public override string Title => Loc.T("diag.title");

        public override bool ShowBack => true;

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g, AppState.Connection.IsConnected ? null : Loc.T("live.offline"));

            float contentTop = top + 20;
            float contentH = H - contentTop - pad;
            float leftW = (W - pad * 3) * 0.42f;

            DrawScoreCard(g, new RectangleF(pad, contentTop, leftW, contentH));
            DrawSystems(g, new RectangleF(pad * 2 + leftW, contentTop, W - leftW - pad * 3, contentH));

            DrawModal(g);
        }

        private void DrawScoreCard(Graphics g, RectangleF bounds)
        {
            Draw.Card(g, bounds, 14f);
            using (var wash = new LinearGradientBrush(RectangleF.Inflate(bounds, 0, 1), Color.FromArgb(46, 238, 24, 52), Color.FromArgb(0, 238, 24, 52), LinearGradientMode.Vertical))
            {
                Draw.FillRounded(g, wash, bounds, 14f);
            }

            int score = AppState.HealthScore;
            float size = Math.Min(bounds.Width - 90, bounds.Height * 0.52f);
            var ring = new RectangleF(bounds.X + (bounds.Width - size) / 2f, bounds.Y + 54, size, size);

            Color from = score >= 80 ? Color.FromArgb(180, 232, 60) : score >= 60 ? Theme.Warn : Theme.Critical;
            Color to = score >= 80 ? Color.FromArgb(24, 200, 120) : score >= 60 ? Color.FromArgb(247, 120, 40) : Color.FromArgb(200, 22, 50);
            Draw.RingGauge(g, ring, score / 100f, from, to, Math.Max(14f, size * 0.075f));

            Draw.TextCentered(g, score.ToString(), Draw.Font(Math.Min(96f, size * 0.42f), FontStyle.Bold), Color.White,
                new RectangleF(ring.X, ring.Y + size * 0.26f, ring.Width, size * 0.36f));
            Draw.TextCentered(g, "/100", Draw.Font(Math.Min(30f, size * 0.13f)), Draw.Alpha(Color.White, 190),
                new RectangleF(ring.X, ring.Y + size * 0.6f, ring.Width, size * 0.16f));

            Draw.TextCentered(g, Loc.T("diag.overall"), Draw.Font(26), Draw.Alpha(Color.White, 220),
                new RectangleF(bounds.X, ring.Bottom + 16, bounds.Width, 36));
            Draw.TextCentered(g, AppState.HealthLabel, Draw.Font(34, FontStyle.Bold), from,
                new RectangleF(bounds.X, ring.Bottom + 52, bounds.Width, 44));

            // Scan state / button.
            var footer = new RectangleF(bounds.X + 22, bounds.Bottom - 132, bounds.Width - 44, 110);

            float scanProgress = ScanProgress;
            if (scanProgress >= 0f)
            {
                Draw.TextIn(g, Loc.T("diag.scanning", ScanSession.Current.CardName),
                    Draw.Font(20, FontStyle.Bold), Color.White,
                    new RectangleF(footer.X, footer.Y, footer.Width, 30), StringAlignment.Near, StringAlignment.Center, false);

                var track = new RectangleF(footer.X, footer.Y + 38, footer.Width, 16);
                Draw.FillRounded(g, Theme.Track, track, 8f);
                Draw.FillRounded(g, Theme.Accent,
                    new RectangleF(track.X, track.Y, Math.Max(16f, track.Width * scanProgress), track.Height), 8f);
                Draw.TextIn(g, $"{scanProgress * 100:0} %", Draw.Font(18), Draw.Alpha(Color.White, 200),
                    new RectangleF(footer.X, track.Bottom + 8, footer.Width, 28), StringAlignment.Far, StringAlignment.Center, false);
            }
            else
            {
                Draw.TextIn(g, $"{Loc.T("diag.lastcheck")}   {AppState.LastScan:yyyy-MM-dd HH:mm}", Draw.Font(19), Draw.Alpha(Color.White, 200),
                    new RectangleF(footer.X, footer.Y, footer.Width, 30), StringAlignment.Near, StringAlignment.Center, false);

                var button = new RectangleF(footer.X, footer.Y + 40, footer.Width, 62);
                DrawButton(g, button, Loc.T("diag.fullscan"), Theme.Accent, Color.White, StartScan, "diag-scan", 16f);
            }
        }

        private void StartScan()
        {
            if (!AppState.Connection.IsConnected)
            {
                OpenModal(Loc.T("diag.offline.title"), Loc.T("diag.offline.body"),
                    Loc.T("diag.offline.ok"), Theme.Accent, () => Shell.Navigate("settings", "connection"));
                return;
            }

            Shell.Navigate("fullscan", "restart");
        }

        private void DrawSystems(Graphics g, RectangleF bounds)
        {
            IReadOnlyList<SystemHealth> systems = AppState.Systems;
            float gap = 12f;
            float rowH = (bounds.Height - gap * (systems.Count - 1)) / systems.Count;

            for (int i = 0; i < systems.Count; i++)
            {
                SystemHealth system = systems[i];
                var row = new RectangleF(bounds.X, bounds.Y + i * (rowH + gap), bounds.Width, rowH);
                string id = $"sys-{system.Name}";
                bool scanning = ScanSession.Running;

                Draw.Card(g, row, 14f, IsHover(id) ? Theme.CardAlt : Theme.Card);

                // Status tile as the scan design draws its modules: tinted glass, bright outline and glyph.
                Color color = AppState.StatusColor(system.Status);
                var iconBox = new RectangleF(row.X + 18, row.Y + (row.Height - 58) / 2f, 58, 58);
                Color glass = Draw.Lerp(color, Theme.Card, 0.7f);
                Draw.FillRounded(g, Draw.Alpha(color, 24), RectangleF.Inflate(iconBox, 4, 4), 15f);
                Draw.FillRounded(g, glass, iconBox, 12f);
                Draw.StrokeRounded(g, Draw.Alpha(color, 180), iconBox, 12f, 1.5f);
                Icons.Draw(g, system.Icon, RectangleF.Inflate(iconBox, -14, -14), Draw.Lerp(color, Color.White, 0.3f), glass);

                Draw.TextIn(g, Loc.SystemName(system.Name), Draw.Font(25, FontStyle.Bold), Theme.Text,
                    new RectangleF(iconBox.Right + 20, row.Y + 12, row.Width * 0.42f, row.Height / 2f),
                    StringAlignment.Near, StringAlignment.Center, false);

                Draw.TextIn(g, scanning ? Loc.T("diag.reading") : system.Detail, Draw.Font(17), Theme.TextSoft,
                    new RectangleF(iconBox.Right + 20, row.Y + row.Height / 2f - 6, row.Width * 0.5f, row.Height / 2f),
                    StringAlignment.Near, StringAlignment.Center, false);

                Draw.TextIn(g, Loc.StatusWord(system.Status), Draw.Font(23, FontStyle.Bold), color,
                    new RectangleF(row.Right - 190, row.Y, 140, row.Height), StringAlignment.Far, StringAlignment.Center, false);

                Draw.Chevron(g, new PointF(row.Right - 28, row.Y + row.Height / 2f), 11f, Theme.TextSoft);

                SystemHealth captured = system;
                Hit(row, () => Shell.Navigate("systemdetail", captured.Name), id);
            }
        }
    }
}

using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Everything known about one fault code, with the freeze frame captured when it set.</summary>
    internal sealed class DtcDetailPage : PageBase
    {
        private DtcRecord? record;

        public override string Title => Loc.T("detail.title");

        public override bool ShowBack => true;

        public override void OnEnter(object? argument)
        {
            if (argument is DtcRecord value)
            {
                record = value;
            }
            else if (argument is string code)
            {
                record = AppState.Dtc.Find(code);
            }

            record ??= AppState.Dtc.All.FirstOrDefault();
            ScrollY = 0;
        }

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);

            if (record is null)
            {
                Draw.TextCentered(g, Loc.T("detail.none"), Draw.Font(24), Theme.TextSoft, new RectangleF(0, top, W, H - top));
                return;
            }

            bool cleared = record.Status == DtcStatus.History;
            float buttonH = cleared ? 0 : 82f;
            var card = new RectangleF(pad, top + 18, W - pad * 2, H - top - 36 - buttonH);

            Draw.Card(g, card, 20f);

            Color severity = Theme.Severity(record.Severity);
            var badge = new RectangleF(card.X + 26, card.Y + 24, 62, 62);
            Draw.FillRounded(g, severity, badge, 14f);
            Draw.WarningTriangle(g, new RectangleF(badge.X + 14, badge.Y + 18, 34, 28), Color.White, severity);

            Draw.Text(g, record.Code, Draw.Font(40, FontStyle.Bold), Theme.Text, badge.Right + 20, card.Y + 30);

            var statusPill = new RectangleF(badge.Right + 190, card.Y + 38, 132, 38);
            Draw.Pill(g, statusPill, Draw.Alpha(severity, 40), Loc.Status(record.Status), Draw.Font(19, FontStyle.Bold), severity);

            float left = card.X + 26;
            float labelW = 210;
            float valueX = left + labelW;
            float valueW = card.Width * 0.54f - labelW;
            float y = card.Y + 112;

            y = Row(g, Loc.T("detail.description"), record.Description, Theme.Accent, left, valueX, valueW, y, labelW);
            y = Row(g, Loc.T("detail.status"), record.Status == DtcStatus.History ? Loc.T("detail.cleared") : Loc.Status(record.Status), Theme.Text, left, valueX, valueW, y, labelW);
            y = Row(g, Loc.T("detail.severity"), Loc.Severity(record.Severity), severity, left, valueX, valueW, y, labelW);
            y = Row(g, Loc.T("detail.system"), Loc.SystemName(record.System), Theme.Text, left, valueX, valueW, y, labelW);
            y = Row(g, Loc.T("detail.detected"), $"{record.DetectedAt:yyyy-MM-dd HH:mm} ({DtcCodesPage.Ago(record.DetectedAt)})", Theme.Text, left, valueX, valueW, y, labelW);

            if (!string.IsNullOrEmpty(record.Effect))
            {
                y = Row(g, Loc.T("detail.effect"), record.Effect, Theme.Text, left, valueX, valueW, y, labelW);
            }

            // Possible causes.
            Draw.TextIn(g, Loc.T("detail.causes"), Draw.Font(21, FontStyle.Bold), Theme.TextSoft,
                new RectangleF(left, y + 6, labelW, 32), StringAlignment.Near, StringAlignment.Near, false);

            float causeY = y + 6;
            foreach (string cause in record.Causes)
            {
                g.FillEllipse(new SolidBrush(Theme.Accent), valueX + 4, causeY + 12, 9, 9);
                Draw.TextIn(g, cause, Draw.Font(21), Theme.Text,
                    new RectangleF(valueX + 24, causeY, valueW - 24, 34), StringAlignment.Near, StringAlignment.Near, false);
                causeY += 36;
            }

            DrawFreezeFrame(g, new RectangleF(card.X + card.Width * 0.58f, card.Y + 112, card.Width * 0.38f, card.Height - 140));

            if (!cleared)
            {
                var button = new RectangleF(W / 2f - 220, H - pad - 62, 440, 58);
                DrawButton(g, button, Loc.T("detail.clear"), Theme.Accent, Color.White, () => OpenModal(
                    Loc.T("detail.clear.title", record.Code),
                    Loc.T("detail.clear.body"),
                    Loc.T("detail.clear.ok"),
                    Theme.Critical,
                    () =>
                    {
                        AppState.Dtc.Clear(record);
                        Shell.RefreshShell();
                    }), "detail-clear", 15f);
            }

            DrawModal(g);
        }

        private static float Row(Graphics g, string label, string value, Color valueColor,
            float labelX, float valueX, float valueW, float y, float labelW)
        {
            Draw.TextIn(g, label, Draw.Font(21, FontStyle.Bold), Theme.TextSoft,
                new RectangleF(labelX, y, labelW, 32), StringAlignment.Near, StringAlignment.Near, false);

            using var format = new StringFormat { Trimming = StringTrimming.Word };
            SizeF size = g.MeasureString(value, Draw.Font(21), (int)valueW, format);
            Draw.TextIn(g, value, Draw.Font(21), valueColor, new RectangleF(valueX, y, valueW, size.Height + 6));

            return y + Math.Max(38f, size.Height + 14f);
        }

        private void DrawFreezeFrame(Graphics g, RectangleF bounds)
        {
            if (record is null || bounds.Width < 180)
            {
                return;
            }

            Draw.FillRounded(g, Theme.CardAlt, bounds, 16f);
            Draw.StrokeRounded(g, Theme.Border, bounds, 16f, 1f);

            Draw.TextIn(g, Loc.T("detail.freeze"), Draw.Font(22, FontStyle.Bold), Theme.Text,
                new RectangleF(bounds.X + 20, bounds.Y + 16, bounds.Width - 40, 32), StringAlignment.Near, StringAlignment.Center, false);
            Draw.TextIn(g, Loc.T("detail.freezehint"), Draw.Font(16), Theme.TextSoft,
                new RectangleF(bounds.X + 20, bounds.Y + 48, bounds.Width - 40, 26), StringAlignment.Near, StringAlignment.Center, false);

            float y = bounds.Y + 86;
            if (record.FreezeFrame.Count == 0)
            {
                Draw.TextIn(g, Loc.T("detail.nofreeze"), Draw.Font(19), Theme.TextSoft,
                    new RectangleF(bounds.X + 20, y, bounds.Width - 40, 60));
                return;
            }

            foreach ((string key, string value) in record.FreezeFrame)
            {
                if (y + 44 > bounds.Bottom - 70)
                {
                    break;
                }

                Draw.TextIn(g, key, Draw.Font(19), Theme.TextSoft,
                    new RectangleF(bounds.X + 20, y, bounds.Width * 0.55f, 34), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, value, Draw.Font(19, FontStyle.Bold), Theme.Text,
                    new RectangleF(bounds.X + bounds.Width * 0.5f, y, bounds.Width * 0.5f - 20, 34), StringAlignment.Far, StringAlignment.Center, false);

                using var pen = new Pen(Theme.Border, 1f);
                g.DrawLine(pen, bounds.X + 20, y + 36, bounds.Right - 20, y + 36);
                y += 44;
            }

            var graph = new RectangleF(bounds.X + 16, bounds.Bottom - 62, bounds.Width - 32, 46);
            DrawGhostButton(g, graph, Loc.T("detail.opengraph"), Theme.Accent,
                () => Shell.Navigate("graph", record.System == "Emission" ? "o2" : "rpm"), "detail-graph", 12f);
        }
    }
}

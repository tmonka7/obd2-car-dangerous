using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Fault code list split into current, pending and history.</summary>
    internal sealed class DtcCodesPage : PageBase
    {
        private int tab;

        public override string Title => Loc.T("dtc.title");

        public override bool ShowBack => true;

        public override void OnEnter(object? argument)
        {
            if (argument is DtcStatus status)
            {
                tab = (int)status;
            }

            ScrollY = 0;
        }

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);

            string[] labels =
            {
                Loc.T("dtc.tab", Loc.T("dtc.current"), AppState.Dtc.Count(DtcStatus.Current)),
                Loc.T("dtc.tab", Loc.T("dtc.pending"), AppState.Dtc.Count(DtcStatus.Pending)),
                Loc.T("dtc.tab", Loc.T("dtc.history"), AppState.Dtc.Count(DtcStatus.History)),
            };

            var tabsRect = new RectangleF(pad, top + 18, W - pad * 2, 58);
            DrawTabs(g, tabsRect, labels, tab, index =>
            {
                tab = index;
                ScrollY = 0;
                Invalidate();
            }, "dtc-tab");

            var status = (DtcStatus)tab;
            List<DtcRecord> records = AppState.Dtc.ByStatus(status).ToList();

            bool showClear = status != DtcStatus.History;
            float listBottom = H - pad - (showClear ? 82f : 0f);
            var list = new RectangleF(pad, tabsRect.Bottom + 18, W - pad * 2 - 14, listBottom - tabsRect.Bottom - 26);

            DrawList(g, list, records, status);

            if (showClear)
            {
                var button = new RectangleF(W / 2f - 220, H - pad - 66, 440, 62);
                bool enabled = records.Count > 0;
                if (enabled)
                {
                    DrawButton(g, button, Loc.T("dtc.clearall"), Theme.Accent, Color.White, () => OpenModal(
                        Loc.T("dtc.clear.title"),
                        Loc.T("dtc.clear.body"),
                        Loc.T("dtc.clear.ok"),
                        Theme.Critical,
                        async () =>
                        {
                            int cleared = await AppState.Dtc.ClearAsync();
                            AppState.MarkScanned();
                            Shell.RefreshShell();
                            if (cleared > 0 && AppState.Settings.AlertSound)
                            {
                                System.Media.SystemSounds.Asterisk.Play();
                            }
                        }), "dtc-clear", 16f);
                }
                else
                {
                    Draw.FillRounded(g, Theme.Dark ? Color.FromArgb(28, 52, 84) : Color.FromArgb(226, 233, 242), button, 16f);
                    Draw.TextCentered(g, Loc.T("dtc.nothing"), Draw.Font(22, FontStyle.Bold), Theme.TextSoft, button);
                }
            }

            DrawModal(g);
        }

        private void DrawList(Graphics g, RectangleF bounds, List<DtcRecord> records, DtcStatus status)
        {
            if (records.Count == 0)
            {
                Draw.Card(g, bounds, 18f);
                Draw.TextCentered(g, status switch
                {
                    DtcStatus.Current => Loc.T("dtc.empty.current"),
                    DtcStatus.Pending => Loc.T("dtc.empty.pending"),
                    _ => Loc.T("dtc.empty.history"),
                }, Draw.Font(22), Theme.TextSoft, bounds);
                return;
            }

            const float rowH = 104f;
            const float gap = 12f;
            float contentHeight = records.Count * (rowH + gap) - gap;
            ScrollMaxY = Math.Max(0, contentHeight - bounds.Height);
            ScrollY = Math.Clamp(ScrollY, 0, ScrollMaxY);

            System.Drawing.Drawing2D.GraphicsState state = g.Save();
            g.SetClip(bounds);

            float y = bounds.Y - ScrollY;
            foreach (DtcRecord record in records)
            {
                var row = new RectangleF(bounds.X, y, bounds.Width, rowH);
                if (row.Bottom >= bounds.Y - 40 && row.Y <= bounds.Bottom + 40)
                {
                    DrawRow(g, row, record);
                }

                y += rowH + gap;
            }

            g.Restore(state);
            DrawScrollbar(g, new RectangleF(bounds.Right + 4, bounds.Y, 8, bounds.Height));
        }

        private void DrawRow(Graphics g, RectangleF row, DtcRecord record)
        {
            string id = $"dtc-row-{record.Code}-{record.Status}";
            Draw.Card(g, row, 16f, IsHover(id) ? Theme.CardAlt : Theme.Card);

            Color severity = Theme.Severity(record.Severity);
            var badge = new RectangleF(row.X + 18, row.Y + (row.Height - 58) / 2f, 58, 58);
            Draw.FillRounded(g, record.Status == DtcStatus.History ? Draw.Alpha(severity, 120) : severity, badge, 12f);
            Draw.WarningTriangle(g, new RectangleF(badge.X + 13, badge.Y + 16, 32, 26), Color.White,
                record.Status == DtcStatus.History ? Draw.Lerp(severity, Theme.Card, 0.5f) : severity);

            Draw.TextIn(g, record.Code, Draw.Font(27, FontStyle.Bold), Theme.Text,
                new RectangleF(badge.Right + 22, row.Y, 150, row.Height), StringAlignment.Near, StringAlignment.Center, false);

            Draw.TextIn(g, record.Description, Draw.Font(21), Theme.Accent,
                new RectangleF(badge.Right + 180, row.Y + 16, row.Width - 520, row.Height - 32), StringAlignment.Near, StringAlignment.Center);

            Draw.TextIn(g, $"{Loc.Severity(record.Severity)} · {Loc.SystemName(record.System)}", Draw.Font(17, FontStyle.Bold), Theme.Severity(record.Severity),
                new RectangleF(row.Right - 300, row.Y + 14, 250, row.Height / 2f - 6), StringAlignment.Far, StringAlignment.Center, false);
            Draw.TextIn(g, Ago(record.DetectedAt), Draw.Font(16), Theme.TextSoft,
                new RectangleF(row.Right - 300, row.Y + row.Height / 2f - 4, 250, row.Height / 2f - 10), StringAlignment.Far, StringAlignment.Center, false);

            Draw.Chevron(g, new PointF(row.Right - 28, row.Y + row.Height / 2f), 11f, Theme.TextSoft);
            Hit(row, () => Shell.Navigate("dtcdetail", record), id);
        }

        internal static string Ago(DateTime time)
        {
            TimeSpan span = DateTime.Now - time;
            if (span.TotalMinutes < 1)
            {
                return Loc.T("common.justnow");
            }

            if (span.TotalHours < 1)
            {
                return Loc.T("common.minago", span.TotalMinutes.ToString("0"));
            }

            if (span.TotalDays < 1)
            {
                return Loc.T("common.hourago", span.TotalHours.ToString("0"));
            }

            return Loc.T("common.dayago", span.TotalDays.ToString("0"));
        }
    }
}

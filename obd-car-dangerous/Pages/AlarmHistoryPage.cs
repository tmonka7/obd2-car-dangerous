using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Chronological log of every alarm, filtered by level.</summary>
    internal sealed class AlarmHistoryPage : PageBase
    {
        private int filter;

        public override string Title => Loc.T("alarms.title");

        public override bool ShowBack => true;

        public override void OnEnter(object? argument) => ScrollY = 0;

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);

            (string Label, Color Color)[] filters =
            {
                (Loc.T("common.all"), Theme.Accent),
                (Loc.T("common.warning"), Theme.Warn),
                (Loc.T("common.critical"), Theme.Critical),
            };

            var tabsRect = new RectangleF(pad, top + 18, W - pad * 2, 58);
            float gap = 14f;
            float tabW = (tabsRect.Width - gap * (filters.Length - 1)) / filters.Length;

            for (int i = 0; i < filters.Length; i++)
            {
                var rect = new RectangleF(tabsRect.X + i * (tabW + gap), tabsRect.Y, tabW, tabsRect.Height);
                bool active = i == filter;
                string id = $"alarm-tab-{i}";
                Color color = filters[i].Color;

                Draw.FillRounded(g, Hovered(active ? color : Theme.Card, id), rect, 14f);
                if (!active)
                {
                    Draw.StrokeRounded(g, Theme.Border, rect, 14f, 1f);
                }

                Color textColor = active
                    ? (i == 1 ? Color.FromArgb(52, 38, 0) : Color.White)
                    : Theme.TextSoft;
                Draw.TextCentered(g, filters[i].Label, Draw.Font(22, FontStyle.Bold), textColor, rect);

                int index = i;
                Hit(rect, () =>
                {
                    filter = index;
                    ScrollY = 0;
                    Invalidate();
                }, id);
            }

            List<AlarmEntry> entries = AppState.Dtc.Alarms
                .Where(a => filter switch
                {
                    1 => a.Level == AlarmLevel.Warning,
                    2 => a.Level == AlarmLevel.Critical,
                    _ => true,
                })
                .ToList();

            var list = new RectangleF(pad, tabsRect.Bottom + 18, W - pad * 2 - 14, H - tabsRect.Bottom - 18 - pad);
            DrawList(g, list, entries);
        }

        private void DrawList(Graphics g, RectangleF bounds, List<AlarmEntry> entries)
        {
            Draw.Card(g, bounds, 18f);

            if (entries.Count == 0)
            {
                Draw.TextCentered(g, Loc.T("alarms.empty"), Draw.Font(21), Theme.TextSoft, bounds);
                ScrollMaxY = 0;
                return;
            }

            const float rowH = 72f;
            var inner = RectangleF.Inflate(bounds, -14, -14);
            ScrollMaxY = Math.Max(0, entries.Count * rowH - inner.Height);
            ScrollY = Math.Clamp(ScrollY, 0, ScrollMaxY);

            System.Drawing.Drawing2D.GraphicsState state = g.Save();
            g.SetClip(inner);

            float y = inner.Y - ScrollY;
            foreach (AlarmEntry entry in entries)
            {
                var row = new RectangleF(inner.X, y, inner.Width, rowH);
                if (row.Bottom >= inner.Y - 20 && row.Y <= inner.Bottom + 20)
                {
                    DrawRow(g, row, entry);
                }

                y += rowH;
            }

            g.Restore(state);
            DrawScrollbar(g, new RectangleF(bounds.Right + 4, bounds.Y + 10, 8, bounds.Height - 20));
        }

        private static string LevelText(AlarmLevel level) => level switch
        {
            AlarmLevel.Critical => Loc.T("common.critical"),
            AlarmLevel.Warning => Loc.T("common.warning"),
            _ => Loc.T("common.info"),
        };

        private void DrawRow(Graphics g, RectangleF row, AlarmEntry entry)
        {
            string id = $"alarm-{entry.Time.Ticks}-{entry.Code}";
            Color level = entry.Level switch
            {
                AlarmLevel.Critical => Theme.Critical,
                AlarmLevel.Warning => Theme.Warn,
                _ => Theme.Good,
            };

            if (IsHover(id))
            {
                Draw.FillRounded(g, Theme.CardAlt, row, 10f);
            }

            Draw.TextIn(g, entry.Time.ToString("MM-dd HH:mm"), Draw.Font(19), Theme.TextSoft,
                new RectangleF(row.X + 14, row.Y, 150, row.Height), StringAlignment.Near, StringAlignment.Center, false);

            Draw.TextIn(g, entry.Code, Draw.Font(21, FontStyle.Bold), Theme.Text,
                new RectangleF(row.X + 176, row.Y, 120, row.Height), StringAlignment.Near, StringAlignment.Center, false);

            Draw.TextIn(g, entry.Description, Draw.Font(20), Theme.Accent,
                new RectangleF(row.X + 306, row.Y, row.Width - 460, row.Height), StringAlignment.Near, StringAlignment.Center, false);

            var pill = new RectangleF(row.Right - 140, row.Y + (row.Height - 34) / 2f, 122, 34);
            Draw.Pill(g, pill, Draw.Alpha(level, 38), LevelText(entry.Level), Draw.Font(17, FontStyle.Bold), level);

            using var pen = new Pen(Theme.Border, 1f);
            g.DrawLine(pen, row.X + 10, row.Bottom, row.Right - 10, row.Bottom);

            DtcRecord? record = AppState.Dtc.Find(entry.Code);
            Hit(row, () =>
            {
                if (record is not null)
                {
                    Shell.Navigate("dtcdetail", record);
                }
            }, id);
        }
    }
}

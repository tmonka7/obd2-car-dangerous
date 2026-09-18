using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Searchable dictionary of the generic OBD2 trouble codes.</summary>
    internal sealed class DictionaryPage : PageBase
    {
        private static readonly (string LabelKey, char Letter)[] Filters =
        {
            ("common.all", '\0'),
            ("dict.powertrain", 'P'),
            ("dict.body", 'B'),
            ("dict.chassis", 'C'),
            ("dict.network", 'U'),
        };

        private readonly System.Windows.Forms.Timer caretTimer = new() { Interval = 500 };
        private string query = string.Empty;
        private int filter;
        private bool caretOn = true;
        private CatalogEntry? selected;

        public DictionaryPage()
        {
            caretTimer.Tick += (_, _) =>
            {
                caretOn = !caretOn;
                Invalidate();
            };
            caretTimer.Start();
        }

        public override string Title => Loc.T("dict.title");

        public override bool ShowBack => true;

        /// <summary>The search field swallows Backspace, so the shell must not treat it as "go back".</summary>
        public override bool WantsTextInput => true;

        public override void OnEnter(object? argument)
        {
            if (argument is string code && code.Length > 0)
            {
                query = code;
                selected = DtcCatalog.Find(code);
            }

            ScrollY = 0;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Back && query.Length > 0)
            {
                query = query[..^1];
                ScrollY = 0;
                e.Handled = true;
                Invalidate();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                query = string.Empty;
                ScrollY = 0;
                e.Handled = true;
                Invalidate();
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (!char.IsControl(e.KeyChar) && query.Length < 40)
            {
                query += char.ToUpperInvariant(e.KeyChar);
                ScrollY = 0;
                e.Handled = true;
                Invalidate();
            }
        }

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);

            List<CatalogEntry> results = DtcCatalog.Search(query, Filters[filter].Letter);
            selected ??= results.FirstOrDefault();

            var search = new RectangleF(pad, top + 18, W - pad * 2, 60);
            DrawSearchBox(g, search);

            var tabs = new RectangleF(pad, search.Bottom + 14, W - pad * 2, 52);
            DrawFilters(g, tabs);

            float listTop = tabs.Bottom + 16;
            float listH = H - listTop - pad;
            float listW = (W - pad * 3) * 0.62f;

            DrawResults(g, new RectangleF(pad, listTop, listW, listH), results);
            DrawDetail(g, new RectangleF(pad * 2 + listW, listTop, W - listW - pad * 3, listH), results.Count);
        }

        private void DrawSearchBox(Graphics g, RectangleF bounds)
        {
            Draw.Card(g, bounds, 16f);

            var icon = new RectangleF(bounds.X + 18, bounds.Y + 15, 30, 30);
            Icons.Draw(g, "search", icon, Theme.TextSoft, Theme.Card);

            float textX = icon.Right + 16;
            bool empty = query.Length == 0;
            Draw.TextIn(g, empty ? Loc.T("dict.search") : query, Draw.Font(24, empty ? FontStyle.Regular : FontStyle.Bold),
                empty ? Theme.TextSoft : Theme.Text,
                new RectangleF(textX, bounds.Y, bounds.Width - 260, bounds.Height), StringAlignment.Near, StringAlignment.Center, false);

            if (caretOn && !empty)
            {
                float caretX = textX + Draw.Measure(g, query, Draw.Font(24, FontStyle.Bold)).Width + 2;
                using var caret = new Pen(Theme.Accent, 2f);
                g.DrawLine(caret, caretX, bounds.Y + 16, caretX, bounds.Bottom - 16);
            }

            if (!empty)
            {
                var clear = new RectangleF(bounds.Right - 54, bounds.Y + 15, 30, 30);
                Icons.Draw(g, "close", clear, Hovered(Theme.TextSoft, "dict-clear"), Theme.Card);
                Hit(new RectangleF(clear.X - 10, bounds.Y + 6, 50, 48), () =>
                {
                    query = string.Empty;
                    ScrollY = 0;
                    Invalidate();
                }, "dict-clear");
            }
            else
            {
                Draw.TextIn(g, Loc.T("dict.hint"), Draw.Font(17), Theme.TextSoft,
                    new RectangleF(bounds.Right - 460, bounds.Y, 436, bounds.Height), StringAlignment.Far, StringAlignment.Center, false);
            }

            Hit(bounds, () => Focus(), "dict-box");
        }

        private void DrawFilters(Graphics g, RectangleF bounds)
        {
            float gap = 12f;
            float width = (bounds.Width - gap * (Filters.Length - 1)) / Filters.Length;

            for (int i = 0; i < Filters.Length; i++)
            {
                var rect = new RectangleF(bounds.X + i * (width + gap), bounds.Y, width, bounds.Height);
                bool active = i == filter;
                string id = $"dict-filter-{i}";

                Draw.FillRounded(g, Hovered(active ? Theme.Accent : Theme.Card, id), rect, 13f);
                if (!active)
                {
                    Draw.StrokeRounded(g, Theme.Border, rect, 13f, 1f);
                }

                Draw.TextCentered(g, Loc.T(Filters[i].LabelKey), Draw.Font(20, FontStyle.Bold),
                    active ? Color.White : Theme.TextSoft, rect);

                int index = i;
                Hit(rect, () =>
                {
                    filter = index;
                    ScrollY = 0;
                    Invalidate();
                }, id);
            }
        }

        private void DrawResults(Graphics g, RectangleF bounds, List<CatalogEntry> results)
        {
            Draw.Card(g, bounds, 18f);

            if (results.Count == 0)
            {
                Draw.TextCentered(g, Loc.T("dict.nomatch"), Draw.Font(21), Theme.TextSoft, bounds);
                ScrollMaxY = 0;
                return;
            }

            const float rowH = 66f;
            var inner = RectangleF.Inflate(bounds, -14, -14);
            ScrollMaxY = Math.Max(0, results.Count * rowH - inner.Height);
            ScrollY = Math.Clamp(ScrollY, 0, ScrollMaxY);

            System.Drawing.Drawing2D.GraphicsState state = g.Save();
            g.SetClip(inner);

            int first = Math.Max(0, (int)(ScrollY / rowH) - 1);
            int last = Math.Min(results.Count - 1, (int)((ScrollY + inner.Height) / rowH) + 1);

            for (int i = first; i <= last; i++)
            {
                CatalogEntry entry = results[i];
                var row = new RectangleF(inner.X, inner.Y - ScrollY + i * rowH, inner.Width, rowH);
                string id = $"dict-row-{entry.Code}";
                bool isSelected = selected?.Code == entry.Code;
                bool stored = AppState.Dtc.Find(entry.Code) is not null;

                if (isSelected)
                {
                    Draw.FillRounded(g, Draw.Alpha(Theme.Accent, 28), row, 10f);
                }
                else if (IsHover(id))
                {
                    Draw.FillRounded(g, Theme.CardAlt, row, 10f);
                }

                Draw.TextIn(g, entry.Code, Draw.Font(22, FontStyle.Bold), CategoryColor(entry.Letter),
                    new RectangleF(row.X + 16, row.Y, 110, row.Height), StringAlignment.Near, StringAlignment.Center, false);

                Draw.TextIn(g, entry.Description, Draw.Font(19), Theme.Text,
                    new RectangleF(row.X + 136, row.Y, row.Width - 190, row.Height), StringAlignment.Near, StringAlignment.Center, false);

                if (stored)
                {
                    var dot = new RectangleF(row.Right - 34, row.Y + row.Height / 2f - 7, 14, 14);
                    using var brush = new SolidBrush(Theme.Critical);
                    g.FillEllipse(brush, dot);
                }

                using var line = new Pen(Theme.Border, 1f);
                g.DrawLine(line, row.X + 12, row.Bottom, row.Right - 12, row.Bottom);

                CatalogEntry captured = entry;
                Hit(row, () =>
                {
                    selected = captured;
                    Invalidate();
                }, id);
            }

            g.Restore(state);
            DrawScrollbar(g, new RectangleF(bounds.Right - 10, bounds.Y + 12, 8, bounds.Height - 24));
        }

        private void DrawDetail(Graphics g, RectangleF bounds, int resultCount)
        {
            Draw.Card(g, bounds, 18f);

            Draw.TextIn(g, Loc.T("dict.count", resultCount, DtcCatalog.Count), Draw.Font(18, FontStyle.Bold), Theme.TextSoft,
                new RectangleF(bounds.X + 22, bounds.Y + 14, bounds.Width - 44, 28), StringAlignment.Near, StringAlignment.Center, false);

            if (selected is null)
            {
                return;
            }

            Color color = CategoryColor(selected.Letter);
            var badge = new RectangleF(bounds.X + 22, bounds.Y + 54, 76, 76);
            Draw.FillRounded(g, color, badge, 16f);
            Draw.TextCentered(g, selected.Letter.ToString(), Draw.Font(38, FontStyle.Bold), Color.White, badge);

            Draw.Text(g, selected.Code, Draw.Font(40, FontStyle.Bold), Theme.Text, badge.Right + 20, bounds.Y + 56);
            Draw.Text(g, Loc.SystemName(selected.Category), Draw.Font(19), Theme.TextSoft, badge.Right + 22, bounds.Y + 104);

            float y = badge.Bottom + 22;
            Draw.TextIn(g, selected.Description, Draw.Font(23, FontStyle.Bold), Theme.Text,
                new RectangleF(bounds.X + 22, y, bounds.Width - 44, 100));

            y += 104;
            Draw.TextIn(g, DtcCatalog.FamilyOf(selected.Code), Draw.Font(19), Theme.TextSoft,
                new RectangleF(bounds.X + 22, y, bounds.Width - 44, 70));

            y += 76;
            DtcRecord? stored = AppState.Dtc.Find(selected.Code);
            if (stored is not null)
            {
                var pill = new RectangleF(bounds.X + 22, y, bounds.Width - 44, 46);
                Draw.Pill(g, pill, Draw.Alpha(Theme.Critical, 34), Loc.T("dict.invehicle"), Draw.Font(19, FontStyle.Bold), Theme.Critical);

                var open = new RectangleF(bounds.X + 22, pill.Bottom + 12, bounds.Width - 44, 52);
                DrawGhostButton(g, open, Loc.T("dict.opendetail"), Theme.Accent,
                    () => Shell.Navigate("dtcdetail", stored), "dict-open", 13f);
            }

            Draw.TextIn(g, Loc.T("dict.english"), Draw.Font(16), Theme.TextSoft,
                new RectangleF(bounds.X + 22, bounds.Bottom - 74, bounds.Width - 44, 60));
        }

        private static Color CategoryColor(char letter) => letter switch
        {
            'P' => Theme.Accent,
            'B' => Theme.Violet,
            'C' => Theme.Orange,
            _ => Color.FromArgb(0, 176, 185),
        };

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                caretTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

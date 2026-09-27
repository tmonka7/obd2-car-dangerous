using System.Drawing.Drawing2D;

namespace obd_car_dangerous.Ui
{
    /// <summary>Where a page can ask the shell to take it next.</summary>
    internal interface IShell
    {
        void Navigate(string page, object? argument = null);

        void Back();

        bool CanGoBack { get; }

        void RefreshShell();
    }

    /// <summary>
    /// Base for every screen. Pages paint in a fixed design space 800 units high and as many units
    /// wide as the window needs, so the layout fills any monitor without letterboxing or distortion.
    /// </summary>
    internal abstract class PageBase : UserControl
    {
        public const float DesignHeight = 800f;

        private readonly List<HitRegion> regions = new();
        private string? hoverId;
        private string? pressedId;

        protected PageBase()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.PageTop;
            Dock = DockStyle.Fill;
        }

        public IShell Shell { get; set; } = null!;

        /// <summary>Page title shown in the header bar.</summary>
        public virtual string Title => string.Empty;

        /// <summary>True when the header should offer a back arrow.</summary>
        public virtual bool ShowBack => false;

        /// <summary>True when the page has a text field, so the shell leaves typing keys alone.</summary>
        public virtual bool WantsTextInput => false;

        /// <summary>Pixels per design unit.</summary>
        protected float S { get; private set; } = 1f;

        /// <summary>Width of the drawing surface in design units.</summary>
        protected float W { get; private set; } = 1280f;

        protected float H => DesignHeight;

        /// <summary>Called when the page becomes visible; use it to refresh cached data.</summary>
        public virtual void OnEnter(object? argument)
        {
        }

        public virtual void OnLeave()
        {
        }

        protected abstract void Render(Graphics g);

        protected sealed override void OnPaint(PaintEventArgs e)
        {
            regions.Clear();

            S = Math.Max(0.2f, ClientSize.Height / DesignHeight);
            W = ClientSize.Width / S;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height)),
                       Theme.PageTop, Theme.PageBottom, LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            GraphicsState state = g.Save();
            g.ScaleTransform(S, S);
            Render(g);
            g.Restore(state);
        }

        // ---- scrolling ---------------------------------------------------

        /// <summary>Current vertical scroll in design units; pages subtract it from their content Y.</summary>
        protected float ScrollY { get; set; }

        /// <summary>Largest allowed scroll; pages set it while laying out a long list.</summary>
        protected float ScrollMaxY { get; set; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ScrollMaxY <= 0)
            {
                return;
            }

            ScrollY = Math.Clamp(ScrollY - e.Delta / 2f, 0, ScrollMaxY);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (CanFocus)
            {
                Focus();
            }
        }

        /// <summary>Thin scrollbar drawn beside a scrollable list.</summary>
        protected void DrawScrollbar(Graphics g, RectangleF track)
        {
            if (ScrollMaxY <= 1)
            {
                return;
            }

            float visible = track.Height / (track.Height + ScrollMaxY);
            float thumbH = Math.Max(48f, track.Height * visible);
            float y = track.Y + (track.Height - thumbH) * (ScrollY / ScrollMaxY);
            Draw.FillRounded(g, Theme.Track, track, track.Width / 2f);
            Draw.FillRounded(g, Draw.Alpha(Theme.Accent, 160), new RectangleF(track.X, y, track.Width, thumbH), track.Width / 2f);
        }

        // ---- hit testing -------------------------------------------------

        protected void Hit(RectangleF designRect, Action onClick, string? id = null)
        {
            regions.Add(new HitRegion(designRect, onClick, id ?? $"r{regions.Count}"));
        }

        protected bool IsHover(string id) => hoverId == id;

        /// <summary>Region under the pointer and region held down, for pages that cache what they paint.</summary>
        protected string? HoverId => hoverId;

        protected string? PressedId => pressedId;

        protected bool IsPressed(string id) => pressedId == id;

        /// <summary>Slightly lightens a colour while the pointer is over the region.</summary>
        protected Color Hovered(Color color, string id)
        {
            if (IsPressed(id))
            {
                return Draw.Lerp(color, Color.Black, 0.12f);
            }

            return IsHover(id) ? Draw.Lerp(color, Color.White, 0.12f) : color;
        }

        protected PointF ToDesign(Point client) => new(client.X / S, client.Y / S);

        private HitRegion? RegionAt(Point client)
        {
            PointF p = ToDesign(client);
            for (int i = regions.Count - 1; i >= 0; i--)
            {
                if (regions[i].Bounds.Contains(p))
                {
                    return regions[i];
                }
            }

            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string? id = RegionAt(e.Location)?.Id;
            if (id != hoverId)
            {
                hoverId = id;
                Cursor = id is null ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverId is not null || pressedId is not null)
            {
                hoverId = null;
                pressedId = null;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            pressedId = RegionAt(e.Location)?.Id;
            if (pressedId is not null)
            {
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            HitRegion? region = RegionAt(e.Location);
            string? wasPressed = pressedId;
            pressedId = null;
            Invalidate();

            if (region is not null && region.Id == wasPressed)
            {
                region.OnClick();
            }
        }

        // ---- shared chrome -----------------------------------------------

        /// <summary>Height of the top bar; the rail's logo cell has the same height so the two read as one bar.</summary>
        public const float TopBarHeight = 78f;

        /// <summary>
        /// Top bar of the design: back button, title with the vehicle line under it, and on the right the
        /// connection pill, the language switch, the clock and the status icons. Returns where content starts.
        /// </summary>
        protected float DrawHeader(Graphics g, string? subtitle = null)
        {
            const float height = TopBarHeight;
            using (var bar = new SolidBrush(Theme.ShellTop))
            {
                g.FillRectangle(bar, 0, 0, W, height);
            }

            using (var line = new Pen(Theme.ShellLine, 1f))
            {
                g.DrawLine(line, 0, height - 0.5f, W, height - 0.5f);
            }

            float x = 24f;
            if (ShowBack)
            {
                // Back: a chevron in a dark rounded square, as beside the design's title.
                var back = new RectangleF(16, 17, 44, 44);
                Draw.FillRounded(g, Hovered(Color.FromArgb(14, 22, 34), "hdr-back"), back, 11f);
                Draw.StrokeRounded(g, Color.FromArgb(28, 40, 56), back, 11f, 1f);
                using (var pen = new Pen(Color.FromArgb(230, 236, 244), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLines(pen, new[] { new PointF(back.X + 25, back.Y + 12), new PointF(back.X + 17, back.Y + 22), new PointF(back.X + 25, back.Y + 32) });
                }

                Hit(new RectangleF(8, 8, 60, 62), () => Shell.Back(), "hdr-back");
                x = 76f;
            }

            float right = DrawStatusCluster(g, height);
            float room = Math.Max(120f, right - 14 - x);

            // Line one: the title, and a page's own status word after it.
            Font titleFont = Draw.Font(24, FontStyle.Bold);
            Draw.TextIn(g, Title, titleFont, Theme.Text, new RectangleF(x, 8, room, 36), StringAlignment.Near, StringAlignment.Center, false);
            if (subtitle is not null)
            {
                float titleW = Math.Min(room, Draw.Measure(g, Title, titleFont).Width);
                Draw.TextIn(g, subtitle, Draw.Font(15), Color.FromArgb(120, 200, 240),
                    new RectangleF(x + titleW + 10, 8, Math.Max(0, room - titleW - 10), 38), StringAlignment.Near, StringAlignment.Center, false);
            }

            // Line two: the vehicle, then a divider and the VIN, as in the design.
            Font lineFont = Draw.Font(15);
            string car = string.Join("  ", new[]
            {
                string.Join(" ", new[] { Services.Vehicle.Make, Services.Vehicle.Model }.Where(s => s != "-")),
                Services.Vehicle.Year, Services.Vehicle.Engine,
            }.Where(s => s.Length > 0 && s != "-"));
            float lx = x + 1;
            if (car.Length > 0)
            {
                Draw.TextIn(g, car, lineFont, Color.FromArgb(187, 210, 232), new RectangleF(lx, 44, room, 26), StringAlignment.Near, StringAlignment.Center, false);
                lx += Draw.Measure(g, car, lineFont).Width + 8;
                using var divider = new Pen(Color.FromArgb(62, 80, 104), 1.2f);
                g.DrawLine(divider, lx, 50, lx, 64);
                lx += 10;
            }

            if (Services.Vehicle.Vin != "-" && lx < x + room - 60)
            {
                Draw.TextIn(g, $"VIN: {Services.Vehicle.Vin}", lineFont, Color.FromArgb(150, 198, 232),
                    new RectangleF(lx, 44, x + room - lx, 26), StringAlignment.Near, StringAlignment.Center, false);
            }

            return height;
        }

        /// <summary>
        /// Right end of the top bar, drawn right to left: status icons, clock, language switch and the
        /// connection pill. Returns the left edge of what it drew.
        /// </summary>
        protected float DrawStatusCluster(Graphics g, float headerHeight)
        {
            float mid = headerHeight / 2f;
            float right = W - 22f;

            Bitmap icons = Pages.Scan.ScanKit.Sprite("hdr-status");
            float iconsH = 21f;
            float iconsW = icons.Width * iconsH / icons.Height;
            g.DrawImage(icons, new RectangleF(right - iconsW, mid - iconsH / 2f - 1, iconsW, iconsH));
            right -= iconsW + 20;

            Font clockFont = Draw.Font(19);
            string clock = DateTime.Now.ToString("HH:mm");
            SizeF clockSize = Draw.Measure(g, clock, clockFont);
            Draw.Text(g, clock, clockFont, Color.FromArgb(234, 241, 247), right - clockSize.Width, mid - clockSize.Height / 2f);
            right -= clockSize.Width + 22;

            right = DrawLanguageSwitch(g, right, mid) - 16;

            // Connection pill: status, then the adapter when there is room for it. Opens the connection settings.
            Services.ConnectionService link = Services.AppState.Connection;
            Color dot = link.IsLive ? Theme.Good : link.IsDemo ? Theme.Warn : Theme.Critical;
            Font statusFont = Draw.Font(14, FontStyle.Bold);
            Font adapterFont = Draw.Font(14);
            string status = link.StatusText;
            string adapter = link.Current.Name;
            float statusW = Draw.Measure(g, status, statusFont).Width;
            float adapterW = Math.Min(130f, Draw.Measure(g, adapter, adapterFont).Width);
            bool showAdapter = W > 1100;
            float pillW = 34 + statusW + (showAdapter ? 20 + 34 + adapterW : 0) + 10;
            var pill = new RectangleF(right - pillW, mid - 16, pillW, 32);
            Draw.FillRounded(g, Hovered(Color.FromArgb(10, 22, 30), "hdr-link"), pill, 12f);
            Draw.StrokeRounded(g, Color.FromArgb(22, 38, 50), pill, 12f, 1f);
            using (var glow = new SolidBrush(Color.FromArgb(50, dot)))
            {
                g.FillEllipse(glow, pill.X + 11, mid - 7, 14, 14);
            }

            using (var brush = new SolidBrush(dot))
            {
                g.FillEllipse(brush, pill.X + 13, mid - 5, 10, 10);
            }

            Draw.TextIn(g, status, statusFont, link.IsLive ? Color.FromArgb(22, 239, 183) : dot,
                new RectangleF(pill.X + 30, pill.Y, statusW + 4, pill.Height), StringAlignment.Near, StringAlignment.Center, false);

            if (showAdapter)
            {
                float ax = pill.X + 34 + statusW + 10;
                using (var sep = new Pen(Color.FromArgb(32, 46, 62), 1f))
                {
                    g.DrawLine(sep, ax, pill.Y + 9, ax, pill.Bottom - 9);
                }

                Bitmap chip = Pages.Scan.ScanKit.Sprite("hdr-adapter");
                g.DrawImage(chip, new RectangleF(ax + 11, mid - 7, 19, 14));
                Draw.TextIn(g, adapter, adapterFont, Color.FromArgb(151, 180, 211),
                    new RectangleF(ax + 36, pill.Y, adapterW + 4, pill.Height), StringAlignment.Near, StringAlignment.Center, false);
            }

            Hit(pill, () => Shell.Navigate("settings", "connection"), "hdr-link");
            return pill.X;
        }

        private static readonly Font JapaneseLabel = new("Yu Gothic UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        private static readonly Font ChineseLabel = new("Microsoft YaHei UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);

        /// <summary>EN / 日本語 / 中文, the selected one in red. Returns its left edge.</summary>
        private float DrawLanguageSwitch(Graphics g, float right, float mid)
        {
            string[] labels = { "EN", "日本語", "中文" };
            float[] widths = { 40, 54, 48 };
            int selected = Array.IndexOf(Services.Loc.Languages, Services.Loc.Language);
            float x = right - widths.Sum() - 4;
            float left = x;

            for (int i = 0; i < labels.Length; i++)
            {
                var cell = new RectangleF(x, mid - 16, widths[i], 32);
                string id = $"hdr-lang-{i}";
                if (i == selected)
                {
                    Draw.GlowFill(g, cell, Theme.Accent, 8f);
                }
                else
                {
                    Draw.FillRounded(g, Hovered(Color.FromArgb(16, 24, 38), id), cell, 8f);
                    Draw.StrokeRounded(g, Color.FromArgb(32, 44, 62), cell, 8f, 1f);
                }

                // Each label in its own script's font: Roboto has no Japanese or Chinese.
                Font font = i == 0 ? Draw.Font(15, FontStyle.Bold) : i == 1 ? JapaneseLabel : ChineseLabel;
                Draw.TextCentered(g, labels[i], font, i == selected ? Color.White : Color.FromArgb(206, 214, 227), cell);

                int index = i;
                Hit(cell, () => Services.AppState.Settings.Update(s => s.Language = Services.Loc.Languages[index]), id);
                x += widths[i] + 2;
            }

            return left;
        }

        /// <summary>Pill shaped tab strip. Returns the bottom edge.</summary>
        protected float DrawTabs(Graphics g, RectangleF bounds, IReadOnlyList<string> tabs, int selected, Action<int> onSelect, string idPrefix)
        {
            if (tabs.Count == 0)
            {
                return bounds.Bottom;
            }

            float gap = 12f;
            float tabWidth = (bounds.Width - gap * (tabs.Count - 1)) / tabs.Count;
            for (int i = 0; i < tabs.Count; i++)
            {
                var rect = new RectangleF(bounds.X + i * (tabWidth + gap), bounds.Y, tabWidth, bounds.Height);
                string id = $"{idPrefix}{i}";
                bool active = i == selected;
                if (active)
                {
                    Draw.GlowFill(g, rect, Hovered(Theme.Accent, id), 10f);
                }
                else
                {
                    Draw.FillRounded(g, Hovered(Theme.Card, id), rect, 10f);
                    Draw.StrokeRounded(g, Theme.Border, rect, 10f, 1f);
                }

                Draw.TextCentered(g, tabs[i], Draw.Font(20, FontStyle.Bold), active ? Color.White : Theme.TextSoft, rect);

                int index = i;
                Hit(rect, () => onSelect(index), id);
            }

            return bounds.Bottom;
        }

        /// <summary>Primary action button.</summary>
        protected void DrawButton(Graphics g, RectangleF bounds, string text, Color fill, Color textColor, Action onClick, string id, float radius = 14f)
        {
            Draw.GlowFill(g, bounds, Hovered(fill, id), Math.Min(radius, 10f));
            Draw.TextCentered(g, text, Draw.Font(21, FontStyle.Bold), textColor, bounds);
            Hit(bounds, onClick, id);
        }

        /// <summary>Outlined secondary button.</summary>
        protected void DrawGhostButton(Graphics g, RectangleF bounds, string text, Color color, Action onClick, string id, float radius = 14f)
        {
            radius = Math.Min(radius, 10f);
            Draw.FillRounded(g, IsHover(id) ? Draw.Alpha(color, 40) : Draw.Alpha(color, 14), bounds, radius);
            Draw.StrokeRounded(g, Draw.Alpha(color, 200), bounds, radius, 1.5f);
            Draw.TextCentered(g, text, Draw.Font(21, FontStyle.Bold), color, bounds);
            Hit(bounds, onClick, id);
        }

        // ---- confirmation dialog ------------------------------------------

        private (string Title, string Body, string Ok, Color Color, Action OnOk)? modal;

        protected bool ModalOpen => modal is not null;

        protected void OpenModal(string title, string body, string okText, Color okColor, Action onOk)
        {
            modal = (title, body, okText, okColor, onOk);
            Invalidate();
        }

        protected void CloseModal()
        {
            modal = null;
            Invalidate();
        }

        /// <summary>Call at the end of Render. Dims the page and shows the pending confirmation.</summary>
        protected void DrawModal(Graphics g)
        {
            if (modal is not { } dialog)
            {
                return;
            }

            // Anything underneath stops responding while the dialog is up.
            regions.Clear();

            using (var dim = new SolidBrush(Color.FromArgb(180, 0, 3, 8)))
            {
                g.FillRectangle(dim, 0, 0, W, H);
            }

            var panel = new RectangleF((W - 620) / 2f, 210, 620, 330);
            Draw.Card(g, panel, 14f);

            Draw.TextIn(g, dialog.Title, Draw.Font(30, FontStyle.Bold), Theme.Text,
                new RectangleF(panel.X + 36, panel.Y + 30, panel.Width - 72, 44), StringAlignment.Near, StringAlignment.Center, false);
            Draw.TextIn(g, dialog.Body, Draw.Font(21), Theme.TextSoft,
                new RectangleF(panel.X + 36, panel.Y + 92, panel.Width - 72, 130));

            var cancel = new RectangleF(panel.X + 36, panel.Bottom - 92, (panel.Width - 88) / 2f, 60);
            var ok = new RectangleF(cancel.Right + 16, cancel.Y, cancel.Width, cancel.Height);

            DrawGhostButton(g, cancel, "Cancel", Theme.TextSoft, CloseModal, "modal-cancel");
            DrawButton(g, ok, dialog.Ok, dialog.Color, Color.White, () =>
            {
                Action action = dialog.OnOk;
                modal = null;
                action();
                Invalidate();
            }, "modal-ok");
        }

        private sealed record HitRegion(RectangleF Bounds, Action OnClick, string Id);
    }
}

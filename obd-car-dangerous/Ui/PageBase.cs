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
            Draw.FillRounded(g, Theme.Dark ? Color.FromArgb(32, 62, 100) : Color.FromArgb(226, 233, 242), track, track.Width / 2f);
            Draw.FillRounded(g, Draw.Alpha(Theme.Accent, 160), new RectangleF(track.X, y, track.Width, thumbH), track.Width / 2f);
        }

        // ---- hit testing -------------------------------------------------

        protected void Hit(RectangleF designRect, Action onClick, string? id = null)
        {
            regions.Add(new HitRegion(designRect, onClick, id ?? $"r{regions.Count}"));
        }

        protected bool IsHover(string id) => hoverId == id;

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

        /// <summary>Draws the blue header bar and returns the Y where page content may start.</summary>
        protected float DrawHeader(Graphics g, string? subtitle = null)
        {
            const float height = 78f;
            using (var bar = new LinearGradientBrush(new RectangleF(0, 0, W, height + 1),
                       Theme.ShellTop, Theme.ShellBottom, LinearGradientMode.Horizontal))
            {
                g.FillRectangle(bar, 0, 0, W, height);
            }

            float x = 26f;
            if (ShowBack)
            {
                var back = new RectangleF(18, 20, 40, 38);
                Icons.Draw(g, "back", back, Hovered(Color.White, "hdr-back"), Theme.ShellTop);
                Hit(new RectangleF(8, 10, 60, 58), () => Shell.Back(), "hdr-back");
                x = 78f;
            }

            Draw.TextIn(g, Title, Draw.Font(30, FontStyle.Bold), Color.White,
                new RectangleF(x, 0, W - x - 260, height), StringAlignment.Near, StringAlignment.Center, false);

            if (subtitle is not null)
            {
                SizeF size = Draw.Measure(g, Title, Draw.Font(30, FontStyle.Bold));
                Draw.TextIn(g, subtitle, Draw.Font(17), Draw.Alpha(Color.White, 190),
                    new RectangleF(x + size.Width + 18, 0, W - x - size.Width - 280, height),
                    StringAlignment.Near, StringAlignment.Center, false);
            }

            DrawStatusCluster(g, height);
            return height;
        }

        /// <summary>Clock, connection icon and battery drawn at the right edge of the header.</summary>
        protected void DrawStatusCluster(Graphics g, float headerHeight)
        {
            float right = W - 24f;
            Font font = Draw.Font(17, FontStyle.Bold);
            string battery = "100%";
            SizeF size = Draw.Measure(g, battery, font);
            var mid = headerHeight / 2f;

            Draw.Text(g, battery, font, Draw.Alpha(Color.White, 225), right - size.Width, mid - size.Height / 2f);
            right -= size.Width + 12;

            var batteryBox = new RectangleF(right - 26, mid - 13, 26, 26);
            Icons.Draw(g, "battery", batteryBox, Draw.Alpha(Color.White, 225), Theme.ShellTop);
            right -= 36;

            var linkBox = new RectangleF(right - 26, mid - 13, 26, 26);
            Icons.Draw(g, Services.AppState.Connection.IsConnected ? "bluetooth" : "wifi", linkBox,
                Services.AppState.Connection.IsConnected ? Color.FromArgb(120, 230, 160) : Draw.Alpha(Color.White, 150), Theme.ShellTop);
            right -= 44;

            string clock = DateTime.Now.ToString("HH:mm");
            SizeF clockSize = Draw.Measure(g, clock, font);
            Draw.Text(g, clock, font, Draw.Alpha(Color.White, 225), right - clockSize.Width, mid - clockSize.Height / 2f);
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
                Color fill = active ? Theme.Accent : Theme.Card;
                Draw.FillRounded(g, Hovered(fill, id), rect, 14f);
                if (!active)
                {
                    Draw.StrokeRounded(g, Theme.Border, rect, 14f, 1f);
                }

                Draw.TextCentered(g, tabs[i], Draw.Font(21, FontStyle.Bold), active ? Color.White : Theme.TextSoft, rect);

                int index = i;
                Hit(rect, () => onSelect(index), id);
            }

            return bounds.Bottom;
        }

        /// <summary>Primary action button.</summary>
        protected void DrawButton(Graphics g, RectangleF bounds, string text, Color fill, Color textColor, Action onClick, string id, float radius = 14f)
        {
            Draw.FillRounded(g, Hovered(fill, id), bounds, radius);
            Draw.TextCentered(g, text, Draw.Font(22, FontStyle.Bold), textColor, bounds);
            Hit(bounds, onClick, id);
        }

        /// <summary>Outlined secondary button.</summary>
        protected void DrawGhostButton(Graphics g, RectangleF bounds, string text, Color color, Action onClick, string id, float radius = 14f)
        {
            if (IsHover(id))
            {
                Draw.FillRounded(g, Draw.Alpha(color, 36), bounds, radius);
            }

            Draw.StrokeRounded(g, color, bounds, radius, 2f);
            Draw.TextCentered(g, text, Draw.Font(22, FontStyle.Bold), color, bounds);
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

            using (var dim = new SolidBrush(Color.FromArgb(150, 4, 14, 30)))
            {
                g.FillRectangle(dim, 0, 0, W, H);
            }

            var panel = new RectangleF((W - 620) / 2f, 210, 620, 330);
            Draw.CardShadow(g, panel, 22f);
            Draw.FillRounded(g, Theme.Card, panel, 22f);

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

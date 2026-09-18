using System.Drawing.Drawing2D;

namespace obd_car_dangerous
{
    public partial class Form1 : Form
    {
        private readonly System.Windows.Forms.Timer progressTimer;
        private float progress = 0.12f;
        private string status = "Starting...";

        public Form1()
        {
            InitializeComponent();

            progressTimer = new System.Windows.Forms.Timer { Interval = 40 };
            progressTimer.Tick += (_, _) =>
            {
                progress += 0.014f;
                status = progress switch
                {
                    < 0.35f => "Starting...",
                    < 0.6f => "Connecting to OBD2 adapter...",
                    < 0.85f => "Reading ECU information...",
                    _ => "Ready",
                };

                if (progress >= 1f)
                {
                    progressTimer.Stop();
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                Invalidate();
            };
            progressTimer.Start();
        }

        /// <summary>Any key or click skips the splash.</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            progress = 1f;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            progress = 1f;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = Math.Min(ClientSize.Width / 1280f, ClientSize.Height / 800f);
            float offsetX = (ClientSize.Width - 1280f * scale) / 2f;
            float offsetY = (ClientSize.Height - 800f * scale) / 2f;

            e.Graphics.TranslateTransform(offsetX, offsetY);
            e.Graphics.ScaleTransform(scale, scale);

            DrawBackground(e.Graphics);
            DrawBrand(e.Graphics);
            DrawCar(e.Graphics);
            DrawProgress(e.Graphics);
        }

        private static void DrawBackground(Graphics graphics)
        {
            using var sky = new LinearGradientBrush(
                new Rectangle(0, 0, 1280, 800),
                Color.FromArgb(135, 204, 247),
                Color.FromArgb(246, 224, 205),
                LinearGradientMode.Vertical);
            graphics.FillRectangle(sky, 0, 0, 1280, 800);

            using var glow = new SolidBrush(Color.FromArgb(55, Color.White));
            graphics.FillEllipse(glow, 120, 100, 420, 190);
            graphics.FillEllipse(glow, 760, 70, 380, 180);

            using var skyline = new SolidBrush(Color.FromArgb(125, 142, 168));
            DrawBuilding(graphics, skyline, 55, 405, 82, 155);
            DrawBuilding(graphics, skyline, 145, 350, 55, 210);
            DrawBuilding(graphics, skyline, 218, 300, 62, 260);
            DrawBuilding(graphics, skyline, 300, 215, 74, 345);
            DrawBuilding(graphics, skyline, 390, 365, 60, 195);
            DrawBuilding(graphics, skyline, 470, 330, 72, 230);
            DrawBuilding(graphics, skyline, 865, 340, 60, 220);
            DrawBuilding(graphics, skyline, 945, 275, 76, 285);
            DrawBuilding(graphics, skyline, 1035, 325, 62, 235);
            DrawBuilding(graphics, skyline, 1120, 255, 80, 305);

            using var windows = new SolidBrush(Color.FromArgb(80, 220, 238, 255));
            for (int x = 65; x < 1190; x += 78)
            {
                for (int y = 330; y < 525; y += 30)
                {
                    graphics.FillRectangle(windows, x, y, 8, 12);
                }
            }

            using var trees = new SolidBrush(Color.FromArgb(47, 108, 78));
            for (int x = 0; x < 1280; x += 80)
            {
                graphics.FillEllipse(trees, x, 505, 110, 80);
                graphics.FillRectangle(trees, x + 48, 555, 12, 45);
            }

            using var road = new LinearGradientBrush(
                new Rectangle(0, 560, 1280, 240),
                Color.FromArgb(82, 118, 153),
                Color.FromArgb(27, 48, 73),
                LinearGradientMode.Vertical);
            graphics.FillRectangle(road, 0, 560, 1280, 240);

            using var lane = new Pen(Color.FromArgb(205, 232, 246), 5);
            graphics.DrawLine(lane, 0, 710, 1280, 650);
            graphics.DrawLine(lane, 0, 800, 1280, 715);
            using var divider = new Pen(Color.FromArgb(190, 218, 236), 7) { DashStyle = DashStyle.Dash };
            graphics.DrawLine(divider, 0, 670, 1280, 620);
        }

        private static void DrawBuilding(Graphics graphics, Brush brush, int x, int y, int width, int height)
        {
            graphics.FillRectangle(brush, x, y, width, height);
            using var roof = new Pen(Color.FromArgb(100, 125, 155), 4);
            graphics.DrawLine(roof, x + width / 2, y - 22, x + width / 2, y);
        }

        private static void DrawBrand(Graphics graphics)
        {
            using var carBlue = new SolidBrush(Color.FromArgb(0, 91, 191));
            using var white = new SolidBrush(Color.White);
            using var red = new SolidBrush(Color.FromArgb(244, 29, 55));
            using var titleFont = new Font("Segoe UI", 76, FontStyle.Bold);
            using var subtitleFont = new Font("Segoe UI", 38, FontStyle.Bold);
            using var taglineFont = new Font("Segoe UI", 27, FontStyle.Bold);

            FillRoundedRectangle(graphics, carBlue, new Rectangle(520, 95, 160, 105), 20);
            graphics.FillPolygon(white, new[] { new Point(545, 95), new Point(570, 65), new Point(630, 65), new Point(655, 95) });
            graphics.FillRectangle(white, 550, 163, 100, 14);
            graphics.FillEllipse(red, 672, 100, 95, 120);
            graphics.FillPolygon(red, new[] { new Point(719, 68), new Point(780, 185), new Point(659, 185) });
            using var exclamationFont = new Font("Segoe UI", 65, FontStyle.Bold);
            graphics.DrawString("!", exclamationFont, white, 704, 86);

            DrawCenteredString(graphics, "OBD2", titleFont, Color.FromArgb(5, 43, 98), 220);
            DrawCenteredString(graphics, "Car Dangerous System", subtitleFont, Color.FromArgb(5, 43, 98), 325);
            DrawCenteredString(graphics, "Monitor  ·  Detect  ·  Protect", taglineFont, Color.FromArgb(8, 66, 135), 400);
        }

        private static void DrawCar(Graphics graphics)
        {
            using var body = new SolidBrush(Color.FromArgb(10, 93, 181));
            using var highlight = new Pen(Color.FromArgb(140, 215, 255), 8);
            using var dark = new SolidBrush(Color.FromArgb(17, 37, 58));

            graphics.FillEllipse(body, 398, 528, 500, 112);
            graphics.FillPolygon(body, new[]
            {
                new Point(435, 565), new Point(530, 490), new Point(675, 455),
                new Point(835, 465), new Point(920, 535), new Point(950, 590),
                new Point(430, 590)
            });
            using var glass = new SolidBrush(Color.FromArgb(29, 73, 118));
            graphics.FillPolygon(glass, new[]
            {
                new Point(548, 493), new Point(675, 468), new Point(812, 477),
                new Point(850, 528), new Point(540, 528)
            });
            graphics.DrawLine(highlight, 470, 552, 560, 532);
            graphics.FillEllipse(dark, 485, 560, 86, 86);
            graphics.FillEllipse(dark, 805, 560, 86, 86);
            using var wheel = new Pen(Color.FromArgb(92, 117, 142), 7);
            graphics.DrawEllipse(wheel, 495, 570, 66, 66);
            graphics.DrawEllipse(wheel, 815, 570, 66, 66);
        }

        private void DrawProgress(Graphics graphics)
        {
            using var track = new SolidBrush(Color.FromArgb(45, 111, 173));
            using var fill = new SolidBrush(Color.FromArgb(0, 174, 243));
            using var statusFont = new Font("Segoe UI", 28, FontStyle.Bold);

            FillRoundedRectangle(graphics, track, new Rectangle(370, 675, 540, 20), 10);
            FillRoundedRectangle(graphics, fill, new Rectangle(370, 675, Math.Max(20, (int)(540 * progress)), 20), 10);
            FillRoundedRectangle(graphics, track, new Rectangle(365, 708, 550, 72), 35);
            DrawCenteredString(graphics, status, statusFont, Color.White, 720);
        }

        private static void FillRoundedRectangle(Graphics graphics, Brush brush, Rectangle bounds, int radius)
        {
            using var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }

        private static void DrawCenteredString(Graphics graphics, string text, Font font, Color color, float y)
        {
            using var brush = new SolidBrush(color);
            SizeF size = graphics.MeasureString(text, font);
            graphics.DrawString(text, font, brush, (1280 - size.Width) / 2, y);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

    }
}

using System.Drawing.Drawing2D;
using obd_car_dangerous.Services;
using obd_car_dangerous.Services.Obd;

namespace obd_car_dangerous
{
    /// <summary>
    /// Startup screen. It is not a timed animation: it runs the real start sequence - look for
    /// adapters, open the ELM327, detect the protocol, read the VIN and the stored fault codes -
    /// and reports each step. If no adapter answers it falls back to the simulated feed.
    /// </summary>
    internal sealed class SplashForm : Form
    {
        private readonly System.Windows.Forms.Timer animation = new() { Interval = 40 };

        private float progress;
        private float target = 0.08f;
        private float blink;
        private string status = "Starting...";
        private bool finished;

        public SplashForm()
        {
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(135, 204, 247);
            ClientSize = new Size(1280, 800);
            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "OBD2 Car Dangerous System";
            WindowState = FormWindowState.Maximized;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            animation.Tick += (_, _) =>
            {
                blink += 0.17f;
                progress += (target - progress) * 0.12f;

                if (finished && progress > 0.985f)
                {
                    animation.Stop();
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                Invalidate();
            };

            Shown += async (_, _) =>
            {
                animation.Start();
                await StartupAsync();
            };
        }

        /// <summary>Endpoint the start sequence connected to, or null when it fell back to demo.</summary>
        public ObdEndpoint? Connected { get; private set; }

        // ---- real start sequence ---------------------------------------------

        private async Task StartupAsync()
        {
            ConnectionService link = AppState.Connection;
            var progressReport = new Progress<string>(text => Report(text, Math.Min(0.9f, target + 0.04f)));

            Report(Loc.T("splash.starting"), 0.12f);
            await Task.Delay(250);

            if (!AppState.Settings.AutoConnect)
            {
                Finish(Loc.T("splash.demo"), demo: true);
                return;
            }

            Report(Loc.T("splash.scanning"), 0.22f);
            await Task.Run(link.RefreshEndpoints);

            // The adapter that worked last time goes first, then anything whose name says OBD -
            // a named dongle is a much better guess than a random COM port. Interfaces that do not
            // speak ELM327 (Autel and friends) are skipped; trying them only wastes time.
            ObdEndpoint[] candidates = link.Found
                .Where(e => e.Kind != EndpointKind.Demo && e.Profile.SpeaksElm327)
                .OrderByDescending(e => e.Address == AppState.Settings.LastAdapter)
                .ThenByDescending(e => AdapterCatalog.LooksLikeAdapter(e.Name))
                .ToArray();
            if (candidates.Length == 0)
            {
                Finish(Loc.T("splash.noadapter"), demo: true);
                return;
            }

            foreach (ObdEndpoint endpoint in candidates)
            {
                Report(Loc.T("splash.connecting.on", endpoint.DisplayName), 0.4f);

                if (await link.ConnectAsync(endpoint, progressReport, quickProbe: true))
                {
                    Connected = link.Current;
                    AppState.Settings.Update(s => s.LastAdapter = link.Current.Address);

                    Report(Loc.T("splash.protocol", link.Link.Protocol), 0.8f);
                    await Task.Delay(200);

                    string vehicle = link.Link.Vin is { Length: > 0 } vin ? vin : Loc.T("splash.novin");
                    Finish(Loc.T("splash.connected", link.Link.Firmware, vehicle), demo: false);
                    return;
                }
            }

            Finish(Loc.T("splash.noadapter"), demo: true);
        }

        private void Report(string text, float targetProgress)
        {
            status = text;
            target = Math.Max(target, targetProgress);
            Invalidate();
        }

        private void Finish(string text, bool demo)
        {
            if (demo)
            {
                AppState.Connection.EnterDemo();
            }

            status = text;
            target = 1f;
            finished = true;
            Invalidate();
        }

        /// <summary>Freezes the splash at a given progress and blink phase, used by the offscreen renderer.</summary>
        internal void PreviewFrame(float progressValue, float blinkPhase)
        {
            animation.Stop();
            progress = progressValue;
            target = progressValue;
            blink = blinkPhase;
            status = Loc.T("splash.connecting");
            Invalidate();
        }

        /// <summary>Any key or click hides the splash; the start sequence carries on behind it.</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            Skip();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Skip();
        }

        private void Skip()
        {
            animation.Stop();
            DialogResult = DialogResult.OK;
            Close();
        }

        // ---- painting --------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
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

        private void DrawBrand(Graphics graphics)
        {
            // 0 = dim, 1 = bright. Drives the blinking warning sign.
            float pulse = 0.5f + (float)Math.Sin(blink) * 0.5f;

            using var carBlue = new SolidBrush(Color.FromArgb(0, 91, 191));
            using var white = new SolidBrush(Color.White);
            using var red = new SolidBrush(Color.FromArgb(244, 29, 55));
            using var redBright = new SolidBrush(Color.FromArgb(
                (int)(214 + 41 * pulse), (int)(22 + 60 * pulse), (int)(44 + 36 * pulse)));

            string family = Loc.FontFamily;
            using var titleFont = new Font(family, 76, FontStyle.Bold);
            using var subtitleFont = new Font(family, 38, FontStyle.Bold);
            using var taglineFont = new Font(family, 27, FontStyle.Bold);

            FillRoundedRectangle(graphics, carBlue, new Rectangle(520, 95, 160, 105), 20);
            graphics.FillPolygon(white, new[] { new Point(545, 95), new Point(570, 65), new Point(630, 65), new Point(655, 95) });
            graphics.FillRectangle(white, 550, 163, 100, 14);

            // Halo behind the warning sign, breathing in and out.
            using (var halo = new SolidBrush(Color.FromArgb((int)(30 + 90 * pulse), 244, 29, 55)))
            {
                float grow = 26f * pulse;
                graphics.FillEllipse(halo, 660 - grow, 60 - grow, 150 + grow * 2, 150 + grow * 2);
            }

            graphics.FillEllipse(red, 672, 100, 95, 120);
            graphics.FillPolygon(redBright, new[] { new Point(719, 68), new Point(780, 185), new Point(659, 185) });
            using var exclamationFont = new Font(family, 65, FontStyle.Bold);
            graphics.DrawString("!", exclamationFont, white, 704, 86);

            DrawCenteredString(graphics, "OBD2", titleFont, Color.FromArgb(5, 43, 98), 220);
            DrawCenteredString(graphics, Loc.T("app.title").Replace("OBD2", string.Empty).Trim(), subtitleFont, Color.FromArgb(5, 43, 98), 325);
            DrawCenteredString(graphics, Loc.T("app.tagline"), taglineFont, Color.FromArgb(8, 66, 135), 400);
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
                new Point(430, 590),
            });
            using var glass = new SolidBrush(Color.FromArgb(29, 73, 118));
            graphics.FillPolygon(glass, new[]
            {
                new Point(548, 493), new Point(675, 468), new Point(812, 477),
                new Point(850, 528), new Point(540, 528),
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
            float pulse = 0.5f + (float)Math.Sin(blink) * 0.5f;

            using var track = new SolidBrush(Color.FromArgb(45, 111, 173));
            using var fill = new SolidBrush(Color.FromArgb(0, 174, 243));
            using var statusFont = new Font(Loc.FontFamily, 26, FontStyle.Bold);

            int barWidth = Math.Max(20, (int)(540 * Math.Clamp(progress, 0f, 1f)));
            FillRoundedRectangle(graphics, track, new Rectangle(370, 675, 540, 20), 10);
            FillRoundedRectangle(graphics, fill, new Rectangle(370, 675, barWidth, 20), 10);

            // Glowing head on the progress bar.
            using (var head = new SolidBrush(Color.FromArgb((int)(90 + 140 * pulse), 210, 245, 255)))
            {
                graphics.FillEllipse(head, 370 + barWidth - 18, 669, 32, 32);
            }

            FillRoundedRectangle(graphics, track, new Rectangle(305, 708, 670, 72), 35);
            DrawCenteredString(graphics, status, statusFont,
                Color.FromArgb((int)(170 + 85 * pulse), 255, 255, 255), 722);

            // Three dots that light up in turn.
            for (int i = 0; i < 3; i++)
            {
                bool lit = (int)(blink * 1.6f) % 3 == i;
                using var dot = new SolidBrush(Color.FromArgb(lit ? 235 : 70, 255, 255, 255));
                graphics.FillEllipse(dot, 604 + i * 36, 640, 16, 16);
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animation.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

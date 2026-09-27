using System.Drawing.Drawing2D;
using obd_car_dangerous.Services;
using obd_car_dangerous.Services.Obd;
using obd_car_dangerous.Ui;

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
            BackColor = Color.FromArgb(2, 6, 12);
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

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            using (var night = new LinearGradientBrush(ClientRectangle.IsEmpty ? new Rectangle(0, 0, 1, 1) : ClientRectangle,
                       Color.FromArgb(6, 13, 24), Color.FromArgb(1, 4, 9), LinearGradientMode.Vertical))
            {
                g.FillRectangle(night, ClientRectangle);
            }

            float scale = Math.Min(ClientSize.Width / 1280f, ClientSize.Height / 800f);
            float offsetX = (ClientSize.Width - 1280f * scale) / 2f;
            float offsetY = (ClientSize.Height - 800f * scale) / 2f;

            g.TranslateTransform(offsetX, offsetY);
            g.ScaleTransform(scale, scale);

            DrawBrand(g);
            DrawCar(g);
            DrawProgress(g);
        }

        /// <summary>The Redline logo and tagline, both as the design sets them.</summary>
        private static void DrawBrand(Graphics g)
        {
            Bitmap logo = Pages.Scan.ScanKit.Sprite("logo");
            const float logoW = 420f;
            float logoH = logo.Height * logoW / logo.Width;
            g.DrawImage(logo, new RectangleF((1280 - logoW) / 2f, 64, logoW, logoH));

            DrawCentered(g, Loc.T("app.title"), Draw.Font(30, FontStyle.Bold), Color.FromArgb(236, 241, 247), 176);
            DrawCentered(g, Loc.T("app.tagline"), Draw.Font(21), Color.FromArgb(146, 170, 199), 220);
        }

        /// <summary>The car of the scan design, over a red floor light that breathes while the start runs.</summary>
        private void DrawCar(Graphics g)
        {
            float pulse = 0.5f + (float)Math.Sin(blink) * 0.5f;
            using (var floor = new GraphicsPath())
            {
                var ellipse = new RectangleF(250, 470, 780, 170);
                floor.AddEllipse(ellipse);
                using var glow = new PathGradientBrush(floor)
                {
                    CenterColor = Color.FromArgb((int)(60 + 60 * pulse), 238, 24, 52),
                    SurroundColors = new[] { Color.FromArgb(0, 238, 24, 52) },
                };
                g.FillEllipse(glow, ellipse);
            }

            Bitmap car = Pages.Scan.ScanKit.Sprite("car");
            const float carW = 780f;
            float carH = car.Height * carW / car.Width;
            g.DrawImage(car, new RectangleF((1280 - carW) / 2f, 250, carW, carH));
        }

        private void DrawProgress(Graphics g)
        {
            float pulse = 0.5f + (float)Math.Sin(blink) * 0.5f;

            var track = new RectangleF(370, 690, 540, 10);
            Draw.FillRounded(g, Color.FromArgb(24, 34, 50), track, 5f);
            Draw.StrokeRounded(g, Color.FromArgb(40, 56, 78), track, 5f, 1f);

            float width = Math.Max(10f, track.Width * Math.Clamp(progress, 0f, 1f));
            var fill = new RectangleF(track.X, track.Y, width, track.Height);
            for (int i = 3; i >= 1; i--)
            {
                Draw.FillRounded(g, Color.FromArgb(22, 255, 20, 50), RectangleF.Inflate(fill, i * 3, i * 3), 5f + i * 3);
            }

            Draw.GradientRounded(g, Color.FromArgb(150, 10, 32), Color.FromArgb(252, 40, 64), fill, 5f, LinearGradientMode.Horizontal);

            // Bright head on the bar, breathing with the floor light.
            using (var head = new SolidBrush(Color.FromArgb((int)(80 + 150 * pulse), 255, 120, 136)))
            {
                g.FillEllipse(head, fill.Right - 9, fill.Y - 4, 18, 18);
            }

            DrawCentered(g, status, Draw.Font(22), Color.FromArgb((int)(180 + 75 * pulse), 230, 236, 244), 718);

            // Three dots that light up in turn.
            for (int i = 0; i < 3; i++)
            {
                bool lit = (int)(blink * 1.6f) % 3 == i;
                using var dot = new SolidBrush(lit ? Color.FromArgb(238, 24, 52) : Color.FromArgb(50, 66, 88));
                g.FillEllipse(dot, 618 + i * 18, 660, 9, 9);
            }
        }

        private static void DrawCentered(Graphics g, string text, Font font, Color color, float y)
        {
            using var brush = new SolidBrush(color);
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (1280 - size.Width) / 2, y);
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

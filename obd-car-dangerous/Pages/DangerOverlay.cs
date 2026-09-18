using System.Drawing.Drawing2D;
using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Full screen red warning shown over everything when a serious fault is detected.</summary>
    internal sealed class DangerOverlay : PageBase
    {
        private readonly System.Windows.Forms.Timer pulseTimer = new() { Interval = 40 };
        private readonly string title;
        private readonly string code;
        private readonly string message;
        private readonly DtcRecord? record;
        private float pulse;

        public DangerOverlay(string title, string code, string message, DtcRecord? record)
        {
            this.title = title;
            this.code = code;
            this.message = message;
            this.record = record;

            pulseTimer.Tick += (_, _) =>
            {
                pulse += 0.045f;
                Invalidate();
            };
            pulseTimer.Start();
        }

        public event EventHandler? Closed;

        public void RequestClose()
        {
            pulseTimer.Stop();
            Closed?.Invoke(this, EventArgs.Empty);
        }

        protected override void Render(Graphics g)
        {
            float glow = 0.5f + (float)Math.Sin(pulse) * 0.5f;

            using (var background = new LinearGradientBrush(
                       new RectangleF(0, 0, Math.Max(1, W), H),
                       Draw.Lerp(Color.FromArgb(214, 32, 48), Color.FromArgb(238, 60, 72), glow),
                       Color.FromArgb(128, 10, 26),
                       LinearGradientMode.Vertical))
            {
                g.FillRectangle(background, 0, 0, W, H);
            }

            // Soft radial glow behind the warning sign.
            var haloBounds = new RectangleF(W / 2f - 520, -40, 1040, 620);
            using (var haloPath = new GraphicsPath())
            {
                haloPath.AddEllipse(haloBounds);
                using var halo = new PathGradientBrush(haloPath)
                {
                    CenterColor = Color.FromArgb((int)(52 + 30 * glow), 255, 255, 255),
                    SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) },
                    CenterPoint = new PointF(W / 2f, 210),
                };
                g.FillPath(halo, haloPath);
            }

            float triangleW = 210f;
            var triangle = new RectangleF(W / 2f - triangleW / 2f, 74, triangleW, 178);
            Draw.WarningTriangle(g, triangle, Color.White, Color.FromArgb(214, 32, 48));

            Draw.TextCentered(g, title, Draw.Font(72, FontStyle.Bold), Color.White,
                new RectangleF(0, triangle.Bottom + 18, W, 90));

            string headline = record is not null
                ? $"{record.Code} - {record.Description}"
                : code == "TEST" || code == "LIMIT" ? message.Split('.')[0] : $"{code} - {message}";

            Draw.TextIn(g, headline, Draw.Font(34, FontStyle.Bold), Color.White,
                new RectangleF(W * 0.12f, triangle.Bottom + 112, W * 0.76f, 96),
                StringAlignment.Center, StringAlignment.Center);

            string detail = record?.Effect ?? message;
            Draw.TextIn(g, detail, Draw.Font(26), Draw.Alpha(Color.White, 225),
                new RectangleF(W * 0.16f, triangle.Bottom + 214, W * 0.68f, 130),
                StringAlignment.Center, StringAlignment.Near);

            if (record is not null)
            {
                Draw.TextCentered(g, $"Severity {record.Severity}  ·  {record.System}  ·  {record.DetectedAt:HH:mm:ss}",
                    Draw.Font(20, FontStyle.Bold), Draw.Alpha(Color.White, 200),
                    new RectangleF(0, triangle.Bottom + 342, W, 34));
            }

            float buttonW = Math.Min(430f, (W - 120) / 2f);
            float buttonY = H - 130;
            var details = new RectangleF(W / 2f - buttonW - 14, buttonY, buttonW, 82);
            var clear = new RectangleF(W / 2f + 14, buttonY, buttonW, 82);

            DrawButton(g, details, "View Details", Hovered(Color.FromArgb(247, 66, 80), "danger-details"), Color.White, () =>
            {
                RequestClose();
                if (record is not null)
                {
                    Shell.Navigate("dtcdetail", record);
                }
                else
                {
                    Shell.Navigate("dtc");
                }
            }, "danger-details", 18f);

            DrawButton(g, clear, record is null ? "Dismiss" : "Clear", Hovered(Color.FromArgb(24, 86, 160), "danger-clear"), Color.White, () =>
            {
                if (record is not null)
                {
                    AppState.Dtc.Clear(record);
                }

                RequestClose();
            }, "danger-clear", 18f);

            Draw.TextCentered(g, "Esc closes this alert", Draw.Font(17), Draw.Alpha(Color.White, 170),
                new RectangleF(0, H - 38, W, 28));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pulseTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

using System.Drawing.Drawing2D;

namespace obd_car_dangerous.Ui
{
    /// <summary>Line and bar charts used by the live data, graph and fuel screens.</summary>
    internal static class Charts
    {
        /// <summary>Filled line chart with axis labels. <paramref name="xLabels"/> is drawn under the plot.</summary>
        public static void Line(Graphics g, RectangleF bounds, float[] values, float min, float max,
            Color stroke, string title, string[] xLabels, bool darkPlot = false)
        {
            Color axis = darkPlot ? Color.FromArgb(150, 180, 215) : Theme.TextSoft;
            Color grid = darkPlot ? Color.FromArgb(40, 74, 120) : Color.FromArgb(232, 238, 246);

            var plot = new RectangleF(bounds.X + 74, bounds.Y + 62, bounds.Width - 96, bounds.Height - 110);

            if (!string.IsNullOrEmpty(title))
            {
                Draw.Text(g, title, Draw.Font(20, FontStyle.Bold), darkPlot ? Color.White : Theme.Text, bounds.X + 12, bounds.Y + 8);
            }

            // Horizontal grid and Y labels.
            using (var gridPen = new Pen(grid, 1f))
            {
                for (int i = 0; i <= 3; i++)
                {
                    float t = i / 3f;
                    float y = plot.Bottom - plot.Height * t;
                    g.DrawLine(gridPen, plot.X, y, plot.Right, y);
                    string label = (min + (max - min) * t).ToString(max - min >= 10 ? "0" : "0.0");
                    Draw.TextIn(g, label, Draw.Font(17), axis,
                        new RectangleF(bounds.X + 4, y - 14, 64, 28), StringAlignment.Far, StringAlignment.Center, false);
                }
            }

            if (values.Length >= 2)
            {
                var points = new PointF[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    float t = (values[i] - min) / Math.Max(0.0001f, max - min);
                    points[i] = new PointF(
                        plot.X + plot.Width * i / (values.Length - 1),
                        plot.Bottom - plot.Height * Math.Clamp(t, 0f, 1f));
                }

                using var path = new GraphicsPath();
                path.AddLines(points);

                using (var fill = new GraphicsPath())
                {
                    fill.AddLines(points);
                    fill.AddLine(points[^1], new PointF(plot.Right, plot.Bottom));
                    fill.AddLine(new PointF(plot.Right, plot.Bottom), new PointF(plot.X, plot.Bottom));
                    fill.CloseFigure();
                    using var brush = new LinearGradientBrush(
                        new RectangleF(plot.X, plot.Y, Math.Max(1, plot.Width), Math.Max(1, plot.Height)),
                        Draw.Alpha(stroke, darkPlot ? 110 : 70), Draw.Alpha(stroke, 0), LinearGradientMode.Vertical);
                    g.FillPath(brush, fill);
                }

                using var pen = new Pen(stroke, 3f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawPath(pen, path);

                using var dot = new SolidBrush(stroke);
                g.FillEllipse(dot, points[^1].X - 5, points[^1].Y - 5, 10, 10);
            }
            else
            {
                Draw.TextCentered(g, Services.Loc.T("live.waiting"), Draw.Font(19), axis, plot);
            }

            for (int i = 0; i < xLabels.Length; i++)
            {
                float x = plot.X + plot.Width * i / Math.Max(1, xLabels.Length - 1);
                Draw.TextIn(g, xLabels[i], Draw.Font(17), axis,
                    new RectangleF(x - 40, plot.Bottom + 8, 80, 28), StringAlignment.Center, StringAlignment.Center, false);
            }
        }

        /// <summary>Vertical bar chart.</summary>
        public static void Bars(Graphics g, RectangleF bounds, float[] values, string[] labels, float max,
            Color color, string title, string unit = "")
        {
            var plot = new RectangleF(bounds.X + 74, bounds.Y + 46, bounds.Width - 96, bounds.Height - 96);

            if (!string.IsNullOrEmpty(title))
            {
                Draw.Text(g, title, Draw.Font(20, FontStyle.Bold), Theme.Text, bounds.X + 12, bounds.Y + 10);
            }

            using (var gridPen = new Pen(Theme.Dark ? Color.FromArgb(40, 74, 120) : Color.FromArgb(232, 238, 246), 1f))
            {
                for (int i = 0; i <= 2; i++)
                {
                    float t = i / 2f;
                    float y = plot.Bottom - plot.Height * t;
                    g.DrawLine(gridPen, plot.X, y, plot.Right, y);
                    Draw.TextIn(g, (max * t).ToString("0"), Draw.Font(17), Theme.TextSoft,
                        new RectangleF(bounds.X + 4, y - 14, 64, 28), StringAlignment.Far, StringAlignment.Center, false);
                }
            }

            if (values.Length == 0)
            {
                return;
            }

            float slot = plot.Width / values.Length;
            float barWidth = Math.Min(64f, slot * 0.46f);
            for (int i = 0; i < values.Length; i++)
            {
                float t = Math.Clamp(values[i] / Math.Max(0.0001f, max), 0f, 1f);
                float h = Math.Max(4f, plot.Height * t);
                var rect = new RectangleF(plot.X + slot * i + (slot - barWidth) / 2f, plot.Bottom - h, barWidth, h);
                Draw.GradientRounded(g, Draw.Lerp(color, Color.White, 0.25f), color, rect, 8f);

                if (i < labels.Length)
                {
                    Draw.TextIn(g, labels[i], Draw.Font(17), Theme.TextSoft,
                        new RectangleF(plot.X + slot * i, plot.Bottom + 8, slot, 28), StringAlignment.Center, StringAlignment.Center, false);
                }
            }

            if (!string.IsNullOrEmpty(unit))
            {
                Draw.TextIn(g, unit, Draw.Font(16), Theme.TextSoft,
                    new RectangleF(bounds.Right - 160, bounds.Y + 12, 148, 24), StringAlignment.Far, StringAlignment.Center, false);
            }
        }

        /// <summary>Resamples a series down to <paramref name="buckets"/> points for plotting.</summary>
        public static float[] Downsample(float[] source, int buckets)
        {
            if (source.Length <= buckets || buckets <= 0)
            {
                return source;
            }

            var result = new float[buckets];
            float step = source.Length / (float)buckets;
            for (int i = 0; i < buckets; i++)
            {
                int start = (int)(i * step);
                int end = Math.Min(source.Length, (int)((i + 1) * step));
                float sum = 0;
                int n = 0;
                for (int j = start; j < end; j++)
                {
                    sum += source[j];
                    n++;
                }

                result[i] = n == 0 ? source[Math.Min(source.Length - 1, start)] : sum / n;
            }

            return result;
        }
    }
}

using System.Drawing.Drawing2D;

namespace obd_car_dangerous.Ui
{
    /// <summary>Vector icons drawn inside a square box so they stay sharp at any resolution.</summary>
    internal static class Icons
    {
        /// <summary>Draws an icon. <paramref name="knockout"/> is the colour behind the icon, used for cut-outs.</summary>
        public static void Draw(Graphics g, string name, RectangleF box, Color color, Color? knockout = null)
        {
            float s = Math.Min(box.Width, box.Height);
            var b = new RectangleF(box.X + (box.Width - s) / 2f, box.Y + (box.Height - s) / 2f, s, s);
            float w = Math.Max(1.6f, s * 0.09f);
            using var pen = new Pen(color, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(color);
            Color hole = knockout ?? Theme.Card;

            switch (name)
            {
                case "home":
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.08f, 0.46f), P(b, 0.5f, 0.12f), P(b, 0.92f, 0.46f),
                    });
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.2f, 0.42f), P(b, 0.2f, 0.9f), P(b, 0.8f, 0.9f), P(b, 0.8f, 0.42f),
                    });
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.4f, 0.9f), P(b, 0.4f, 0.62f), P(b, 0.6f, 0.62f), P(b, 0.6f, 0.9f),
                    });
                    break;

                case "engine":
                case "diagnostics":
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.12f, 0.72f), P(b, 0.12f, 0.4f), P(b, 0.26f, 0.4f), P(b, 0.34f, 0.26f),
                        P(b, 0.58f, 0.26f), P(b, 0.66f, 0.4f), P(b, 0.8f, 0.4f), P(b, 0.8f, 0.55f),
                        P(b, 0.92f, 0.55f), P(b, 0.92f, 0.72f), P(b, 0.12f, 0.72f),
                    });
                    g.DrawLine(pen, P(b, 0.36f, 0.26f), P(b, 0.36f, 0.14f));
                    g.DrawLine(pen, P(b, 0.28f, 0.14f), P(b, 0.46f, 0.14f));
                    break;

                case "chart":
                case "livedata":
                    g.DrawLines(pen, new[] { P(b, 0.12f, 0.14f), P(b, 0.12f, 0.86f), P(b, 0.9f, 0.86f) });
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.24f, 0.68f), P(b, 0.42f, 0.44f), P(b, 0.58f, 0.58f), P(b, 0.84f, 0.24f),
                    });
                    g.DrawLines(pen, new[] { P(b, 0.66f, 0.24f), P(b, 0.86f, 0.24f), P(b, 0.86f, 0.44f) });
                    break;

                case "alert":
                case "dtc":
                    Draw2.Triangle(g, brush, pen, b, hole);
                    break;

                case "settings":
                    Draw2.Gear(g, brush, b, color, hole);
                    break;

                case "fuel":
                    g.DrawRectangle(pen, b.X + s * 0.14f, b.Y + s * 0.14f, s * 0.46f, s * 0.72f);
                    g.DrawLine(pen, P(b, 0.14f, 0.36f), P(b, 0.6f, 0.36f));
                    g.DrawLines(pen, new[] { P(b, 0.6f, 0.34f), P(b, 0.84f, 0.34f), P(b, 0.84f, 0.66f), P(b, 0.72f, 0.66f) });
                    g.DrawLine(pen, P(b, 0.7f, 0.2f), P(b, 0.84f, 0.34f));
                    break;

                case "clock":
                    g.DrawEllipse(pen, b.X + s * 0.1f, b.Y + s * 0.1f, s * 0.8f, s * 0.8f);
                    g.DrawLines(pen, new[] { P(b, 0.5f, 0.28f), P(b, 0.5f, 0.52f), P(b, 0.7f, 0.64f) });
                    break;

                case "pin":
                    g.DrawArc(pen, b.X + s * 0.18f, b.Y + s * 0.08f, s * 0.64f, s * 0.64f, 150, 240);
                    g.DrawLines(pen, new[] { P(b, 0.2f, 0.52f), P(b, 0.5f, 0.92f), P(b, 0.8f, 0.52f) });
                    g.DrawEllipse(pen, b.X + s * 0.38f, b.Y + s * 0.28f, s * 0.24f, s * 0.24f);
                    break;

                case "speed":
                    g.DrawArc(pen, b.X + s * 0.1f, b.Y + s * 0.16f, s * 0.8f, s * 0.8f, 180, 180);
                    g.DrawLine(pen, P(b, 0.5f, 0.56f), P(b, 0.72f, 0.34f));
                    g.FillEllipse(brush, b.X + s * 0.44f, b.Y + s * 0.5f, s * 0.12f, s * 0.12f);
                    for (int i = 0; i <= 4; i++)
                    {
                        double a = Math.PI + Math.PI * i / 4.0;
                        var c = new PointF(b.X + s * 0.5f, b.Y + s * 0.56f);
                        float r1 = s * 0.38f;
                        float r2 = s * 0.3f;
                        g.DrawLine(pen,
                            new PointF(c.X + (float)Math.Cos(a) * r1, c.Y + (float)Math.Sin(a) * r1),
                            new PointF(c.X + (float)Math.Cos(a) * r2, c.Y + (float)Math.Sin(a) * r2));
                    }

                    break;

                case "car":
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.08f, 0.66f), P(b, 0.16f, 0.44f), P(b, 0.3f, 0.28f), P(b, 0.7f, 0.28f),
                        P(b, 0.84f, 0.44f), P(b, 0.92f, 0.66f),
                    });
                    g.DrawLine(pen, P(b, 0.08f, 0.66f), P(b, 0.92f, 0.66f));
                    g.FillEllipse(brush, b.X + s * 0.18f, b.Y + s * 0.6f, s * 0.16f, s * 0.16f);
                    g.FillEllipse(brush, b.X + s * 0.66f, b.Y + s * 0.6f, s * 0.16f, s * 0.16f);
                    break;

                case "battery":
                    g.DrawRectangle(pen, b.X + s * 0.1f, b.Y + s * 0.3f, s * 0.8f, s * 0.48f);
                    g.DrawLine(pen, P(b, 0.28f, 0.3f), P(b, 0.28f, 0.18f));
                    g.DrawLine(pen, P(b, 0.72f, 0.3f), P(b, 0.72f, 0.18f));
                    g.DrawLine(pen, P(b, 0.24f, 0.54f), P(b, 0.4f, 0.54f));
                    g.DrawLine(pen, P(b, 0.6f, 0.54f), P(b, 0.76f, 0.54f));
                    g.DrawLine(pen, P(b, 0.68f, 0.46f), P(b, 0.68f, 0.62f));
                    break;

                case "bluetooth":
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.28f, 0.32f), P(b, 0.72f, 0.68f), P(b, 0.5f, 0.88f), P(b, 0.5f, 0.12f),
                        P(b, 0.72f, 0.32f), P(b, 0.28f, 0.68f),
                    });
                    break;

                case "shield":
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.5f, 0.1f), P(b, 0.86f, 0.26f), P(b, 0.8f, 0.6f), P(b, 0.5f, 0.9f),
                        P(b, 0.2f, 0.6f), P(b, 0.14f, 0.26f), P(b, 0.5f, 0.1f),
                    });
                    break;

                case "bell":
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.2f, 0.7f), P(b, 0.28f, 0.58f), P(b, 0.28f, 0.42f),
                    });
                    g.DrawArc(pen, b.X + s * 0.28f, b.Y + s * 0.16f, s * 0.44f, s * 0.44f, 180, 180);
                    g.DrawLines(pen, new[] { P(b, 0.72f, 0.42f), P(b, 0.72f, 0.58f), P(b, 0.8f, 0.7f), P(b, 0.2f, 0.7f) });
                    g.DrawArc(pen, b.X + s * 0.4f, b.Y + s * 0.68f, s * 0.2f, s * 0.2f, 0, 180);
                    break;

                case "ruler":
                    g.DrawRectangle(pen, b.X + s * 0.1f, b.Y + s * 0.34f, s * 0.8f, s * 0.32f);
                    for (int i = 1; i <= 3; i++)
                    {
                        float x = b.X + s * (0.1f + 0.2f * i);
                        g.DrawLine(pen, new PointF(x, b.Y + s * 0.34f), new PointF(x, b.Y + s * 0.48f));
                    }

                    break;

                case "info":
                    g.DrawEllipse(pen, b.X + s * 0.1f, b.Y + s * 0.1f, s * 0.8f, s * 0.8f);
                    g.FillEllipse(brush, b.X + s * 0.44f, b.Y + s * 0.26f, s * 0.12f, s * 0.12f);
                    g.DrawLine(pen, P(b, 0.5f, 0.46f), P(b, 0.5f, 0.72f));
                    break;

                case "history":
                    g.DrawArc(pen, b.X + s * 0.12f, b.Y + s * 0.12f, s * 0.76f, s * 0.76f, 60, 300);
                    g.DrawLines(pen, new[] { P(b, 0.16f, 0.12f), P(b, 0.28f, 0.28f), P(b, 0.1f, 0.34f) });
                    g.DrawLines(pen, new[] { P(b, 0.5f, 0.32f), P(b, 0.5f, 0.54f), P(b, 0.68f, 0.64f) });
                    break;

                case "search":
                    g.DrawEllipse(pen, b.X + s * 0.14f, b.Y + s * 0.14f, s * 0.52f, s * 0.52f);
                    g.DrawLine(pen, P(b, 0.63f, 0.63f), P(b, 0.88f, 0.88f));
                    break;

                case "book":
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.5f, 0.24f), P(b, 0.5f, 0.86f),
                    });
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.5f, 0.24f), P(b, 0.2f, 0.14f), P(b, 0.1f, 0.18f), P(b, 0.1f, 0.78f),
                        P(b, 0.2f, 0.74f), P(b, 0.5f, 0.86f),
                    });
                    g.DrawLines(pen, new[]
                    {
                        P(b, 0.5f, 0.24f), P(b, 0.8f, 0.14f), P(b, 0.9f, 0.18f), P(b, 0.9f, 0.78f),
                        P(b, 0.8f, 0.74f), P(b, 0.5f, 0.86f),
                    });
                    break;

                case "menu":
                    g.DrawLine(pen, P(b, 0.15f, 0.3f), P(b, 0.85f, 0.3f));
                    g.DrawLine(pen, P(b, 0.15f, 0.5f), P(b, 0.85f, 0.5f));
                    g.DrawLine(pen, P(b, 0.15f, 0.7f), P(b, 0.85f, 0.7f));
                    break;

                case "close":
                    g.DrawLine(pen, P(b, 0.22f, 0.22f), P(b, 0.78f, 0.78f));
                    g.DrawLine(pen, P(b, 0.78f, 0.22f), P(b, 0.22f, 0.78f));
                    break;

                case "back":
                    g.DrawLine(pen, P(b, 0.16f, 0.5f), P(b, 0.86f, 0.5f));
                    g.DrawLines(pen, new[] { P(b, 0.42f, 0.24f), P(b, 0.16f, 0.5f), P(b, 0.42f, 0.76f) });
                    break;

                case "wifi":
                    for (int i = 0; i < 3; i++)
                    {
                        float k = 0.2f + i * 0.13f;
                        g.DrawArc(pen, b.X + s * (0.5f - k), b.Y + s * (0.78f - k), s * k * 2, s * k * 2, 200, 140);
                    }

                    g.FillEllipse(brush, b.X + s * 0.42f, b.Y + s * 0.7f, s * 0.16f, s * 0.16f);
                    break;

                case "transmission":
                    g.DrawLine(pen, P(b, 0.2f, 0.2f), P(b, 0.8f, 0.2f));
                    g.DrawLine(pen, P(b, 0.2f, 0.2f), P(b, 0.2f, 0.62f));
                    g.DrawLine(pen, P(b, 0.5f, 0.2f), P(b, 0.5f, 0.62f));
                    g.DrawLine(pen, P(b, 0.8f, 0.2f), P(b, 0.8f, 0.5f));
                    g.DrawLine(pen, P(b, 0.5f, 0.5f), P(b, 0.8f, 0.5f));
                    g.FillEllipse(brush, b.X + s * 0.42f, b.Y + s * 0.62f, s * 0.16f, s * 0.16f);
                    break;

                case "brake":
                    g.DrawEllipse(pen, b.X + s * 0.12f, b.Y + s * 0.12f, s * 0.76f, s * 0.76f);
                    g.DrawEllipse(pen, b.X + s * 0.3f, b.Y + s * 0.3f, s * 0.4f, s * 0.4f);
                    for (int i = 0; i < 4; i++)
                    {
                        double a = Math.PI / 4 + i * Math.PI / 2;
                        var c = new PointF(b.X + s * 0.5f, b.Y + s * 0.5f);
                        g.DrawLine(pen,
                            new PointF(c.X + (float)Math.Cos(a) * s * 0.2f, c.Y + (float)Math.Sin(a) * s * 0.2f),
                            new PointF(c.X + (float)Math.Cos(a) * s * 0.38f, c.Y + (float)Math.Sin(a) * s * 0.38f));
                    }

                    break;

                case "airbag":
                    g.DrawEllipse(pen, b.X + s * 0.34f, b.Y + s * 0.1f, s * 0.56f, s * 0.56f);
                    g.DrawArc(pen, b.X + s * 0.08f, b.Y + s * 0.42f, s * 0.44f, s * 0.44f, 300, 250);
                    g.DrawLine(pen, P(b, 0.12f, 0.86f), P(b, 0.48f, 0.86f));
                    break;

                default:
                    g.DrawEllipse(pen, b.X + s * 0.15f, b.Y + s * 0.15f, s * 0.7f, s * 0.7f);
                    break;
            }
        }

        private static PointF P(RectangleF b, float x, float y) =>
            new(b.X + b.Width * x, b.Y + b.Height * y);

        private static class Draw2
        {
            public static void Triangle(Graphics g, Brush brush, Pen pen, RectangleF b, Color knockout)
            {
                using var path = new GraphicsPath();
                path.AddPolygon(new[] { P(b, 0.5f, 0.12f), P(b, 0.95f, 0.85f), P(b, 0.05f, 0.85f) });
                using var round = new Pen(pen.Color, b.Width * 0.12f) { LineJoin = LineJoin.Round };
                g.DrawPath(round, path);
                g.FillPath(brush, path);

                using var hole = new SolidBrush(knockout);
                float barW = b.Width * 0.09f;
                obd_car_dangerous.Ui.Draw.FillRounded(g, hole, new RectangleF(b.X + b.Width * 0.5f - barW / 2f, b.Y + b.Height * 0.36f, barW, b.Height * 0.28f), barW / 2f);
                g.FillEllipse(hole, b.X + b.Width * 0.5f - barW * 0.62f, b.Y + b.Height * 0.69f, barW * 1.24f, barW * 1.24f);
            }

            public static void Gear(Graphics g, Brush brush, RectangleF b, Color color, Color knockout)
            {
                float s = b.Width;
                var center = new PointF(b.X + s / 2f, b.Y + s / 2f);
                using var path = new GraphicsPath();
                const int teeth = 8;
                var points = new List<PointF>();
                for (int i = 0; i < teeth * 2; i++)
                {
                    double a = i * Math.PI / teeth - Math.PI / 2;
                    float r = (i % 2 == 0) ? s * 0.46f : s * 0.34f;
                    points.Add(new PointF(center.X + (float)Math.Cos(a) * r, center.Y + (float)Math.Sin(a) * r));
                }

                path.AddPolygon(points.ToArray());
                using var round = new Pen(color, s * 0.1f) { LineJoin = LineJoin.Round };
                g.DrawPath(round, path);
                g.FillPath(brush, path);

                var hole = new RectangleF(center.X - s * 0.15f, center.Y - s * 0.15f, s * 0.3f, s * 0.3f);
                using var eraser = new SolidBrush(knockout);
                g.FillEllipse(eraser, hole);
            }

            private static PointF P(RectangleF b, float x, float y) => new(b.X + b.Width * x, b.Y + b.Height * y);
        }
    }
}

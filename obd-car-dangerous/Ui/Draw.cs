using System.Drawing.Drawing2D;

namespace obd_car_dangerous.Ui
{
    /// <summary>GDI+ helpers shared by every page, plus a font cache so painting never leaks handles.</summary>
    internal static class Draw
    {
        private static readonly Dictionary<(int, FontStyle), Font> FontCache = new();

        static Draw()
        {
            // Japanese and Chinese need a different family, so drop the cached fonts on a switch.
            Services.Loc.Changed += (_, _) =>
            {
                foreach (Font cached in FontCache.Values)
                {
                    cached.Dispose();
                }

                FontCache.Clear();
            };
        }

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            var key = ((int)Math.Round(size * 4), style);
            if (!FontCache.TryGetValue(key, out Font? font))
            {
                font = new Font(Services.Loc.FontFamily, size, style, GraphicsUnit.Pixel);
                FontCache[key] = font;
            }

            return font;
        }

        public static GraphicsPath RoundedPath(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
            if (radius <= 0.1f)
            {
                path.AddRectangle(bounds);
                return path;
            }

            float d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Color color, RectangleF bounds, float radius)
        {
            using var path = RoundedPath(bounds, radius);
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        public static void FillRounded(Graphics g, Brush brush, RectangleF bounds, float radius)
        {
            using var path = RoundedPath(bounds, radius);
            g.FillPath(brush, path);
        }

        public static void StrokeRounded(Graphics g, Color color, RectangleF bounds, float radius, float width)
        {
            using var path = RoundedPath(bounds, radius);
            using var pen = new Pen(color, width);
            g.DrawPath(pen, path);
        }

        public static void GradientRounded(Graphics g, Color from, Color to, RectangleF bounds, float radius, LinearGradientMode mode = LinearGradientMode.Vertical)
        {
            using var path = RoundedPath(bounds, radius);
            using var brush = new LinearGradientBrush(Rectangle.Round(RectangleF.Inflate(bounds, 1, 1)), from, to, mode);
            g.FillPath(brush, path);
        }

        /// <summary>Soft drop shadow underneath a card.</summary>
        public static void CardShadow(Graphics g, RectangleF bounds, float radius)
        {
            Color shadow = Theme.Shadow;
            for (int i = 6; i >= 1; i--)
            {
                var layer = RectangleF.Inflate(bounds, i, i);
                layer.Offset(0, i * 0.6f);
                using var path = RoundedPath(layer, radius + i);
                using var brush = new SolidBrush(Color.FromArgb(Math.Max(3, shadow.A / (i * 3)), shadow.R, shadow.G, shadow.B));
                g.FillPath(brush, path);
            }
        }

        public static void Card(Graphics g, RectangleF bounds, float radius = 18f, Color? fill = null, bool shadow = true)
        {
            if (shadow)
            {
                CardShadow(g, bounds, radius);
            }

            FillRounded(g, fill ?? Theme.Card, bounds, radius);
            StrokeRounded(g, Theme.Border, bounds, radius, 1f);
        }

        public static void Text(Graphics g, string text, Font font, Color color, float x, float y)
        {
            using var brush = new SolidBrush(color);
            g.DrawString(text, font, brush, x, y);
        }

        public static void TextCentered(Graphics g, string text, Font font, Color color, RectangleF bounds)
        {
            using var brush = new SolidBrush(color);
            using var format = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
            };
            g.DrawString(text, font, brush, bounds, format);
        }

        public static void TextIn(Graphics g, string text, Font font, Color color, RectangleF bounds,
            StringAlignment horizontal = StringAlignment.Near, StringAlignment vertical = StringAlignment.Near, bool wrap = true)
        {
            using var brush = new SolidBrush(color);
            using var format = new StringFormat(wrap ? StringFormatFlags.NoClip : StringFormatFlags.NoWrap)
            {
                Alignment = horizontal,
                LineAlignment = vertical,
                Trimming = StringTrimming.EllipsisWord,
            };
            g.DrawString(text, font, brush, bounds, format);
        }

        public static SizeF Measure(Graphics g, string text, Font font) => g.MeasureString(text, font);

        /// <summary>Half-moon gauge used by the Live Data tiles.</summary>
        public static void ArcGauge(Graphics g, RectangleF bounds, float fraction, Color color, float thickness)
        {
            fraction = Math.Clamp(fraction, 0f, 1f);
            using var track = new Pen(Theme.Dark ? Color.FromArgb(46, 78, 118) : Color.FromArgb(225, 232, 241), thickness)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawArc(track, bounds, 180, 180);

            if (fraction > 0.001f)
            {
                using var pen = new Pen(color, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, bounds, 180, 180 * fraction);
            }
        }

        /// <summary>Full ring used by the vehicle health score.</summary>
        public static void RingGauge(Graphics g, RectangleF bounds, float fraction, Color from, Color to, float thickness)
        {
            fraction = Math.Clamp(fraction, 0f, 1f);
            using var track = new Pen(Theme.Dark ? Color.FromArgb(28, 58, 95) : Color.FromArgb(226, 234, 244), thickness)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawEllipse(track, bounds);

            if (fraction <= 0.001f)
            {
                return;
            }

            const float start = -90f;
            float sweep = 360f * fraction;
            int steps = Math.Max(2, (int)(sweep / 6f));
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps;
                float t1 = (i + 1) / (float)steps;
                Color c = Lerp(from, to, t0);
                using var pen = new Pen(c, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, bounds, start + sweep * t0, sweep * (t1 - t0) + 0.6f);
            }
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color color, int alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

        /// <summary>Chevron pointing right, used at the end of list rows.</summary>
        public static void Chevron(Graphics g, PointF center, float size, Color color, float width = 3f)
        {
            using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[]
            {
                new PointF(center.X - size * 0.3f, center.Y - size),
                new PointF(center.X + size * 0.45f, center.Y),
                new PointF(center.X - size * 0.3f, center.Y + size),
            });
        }

        public static void CheckMark(Graphics g, RectangleF bounds, Color color, float width)
        {
            using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[]
            {
                new PointF(bounds.X + bounds.Width * 0.22f, bounds.Y + bounds.Height * 0.54f),
                new PointF(bounds.X + bounds.Width * 0.43f, bounds.Y + bounds.Height * 0.74f),
                new PointF(bounds.X + bounds.Width * 0.79f, bounds.Y + bounds.Height * 0.28f),
            });
        }

        /// <summary>Warning triangle with an exclamation mark.</summary>
        public static void WarningTriangle(Graphics g, RectangleF bounds, Color fill, Color glyph)
        {
            using var path = new GraphicsPath();
            float r = bounds.Width * 0.06f;
            var top = new PointF(bounds.X + bounds.Width / 2f, bounds.Y);
            var left = new PointF(bounds.X, bounds.Bottom);
            var right = new PointF(bounds.Right, bounds.Bottom);
            path.AddLine(top, right);
            path.AddLine(right, left);
            path.CloseFigure();

            using var brush = new SolidBrush(fill);
            using var pen = new Pen(fill, r * 2) { LineJoin = LineJoin.Round };
            g.FillPath(brush, path);
            g.DrawPath(pen, path);

            float barW = bounds.Width * 0.09f;
            float barTop = bounds.Y + bounds.Height * 0.34f;
            float barBottom = bounds.Y + bounds.Height * 0.68f;
            using var glyphBrush = new SolidBrush(glyph);
            FillRounded(g, glyphBrush, new RectangleF(top.X - barW / 2f, barTop, barW, barBottom - barTop), barW / 2f);
            g.FillEllipse(glyphBrush, top.X - barW * 0.62f, barBottom + bounds.Height * 0.05f, barW * 1.24f, barW * 1.24f);
        }

        public static void Pill(Graphics g, RectangleF bounds, Color fill, string text, Font font, Color textColor)
        {
            FillRounded(g, fill, bounds, bounds.Height / 2f);
            TextCentered(g, text, font, textColor, bounds);
        }

        /// <summary>Rounded on/off switch.</summary>
        public static void ToggleSwitch(Graphics g, RectangleF bounds, bool on)
        {
            Color track = on ? Theme.Accent : (Theme.Dark ? Color.FromArgb(52, 78, 112) : Color.FromArgb(206, 214, 226));
            FillRounded(g, track, bounds, bounds.Height / 2f);
            float pad = bounds.Height * 0.09f;
            float d = bounds.Height - pad * 2;
            float x = on ? bounds.Right - d - pad : bounds.X + pad;
            using var knob = new SolidBrush(Color.White);
            using var knobShadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
            g.FillEllipse(knobShadow, x, bounds.Y + pad + 1.5f, d, d);
            g.FillEllipse(knob, x, bounds.Y + pad, d, d);
        }
    }
}

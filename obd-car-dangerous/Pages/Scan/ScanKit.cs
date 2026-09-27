using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using obd_car_dangerous.Services;

namespace obd_car_dangerous.Pages.Scan
{
    /// <summary>
    /// One text of the mock-up: pen position, right edge and centre of the ink, baseline, Roboto size,
    /// weight, horizontal squeeze and colour - all in mock-up pixels.
    /// </summary>
    internal sealed record TextSpec(float X, float Right, float Center, float Baseline, float Size, bool Medium, float ScaleX, Color Color);

    internal enum TextAlign
    {
        Left,
        Right,
        Center,
    }

    /// <summary>Fonts, sprites and drawing helpers for the Full System Scan screen.</summary>
    internal static class ScanKit
    {
        private const int HaloAlpha = 60;

        private static readonly Dictionary<(int, bool), Font> RobotoCache = new();
        private static readonly Dictionary<(int, bool), Font> CjkCache = new();
        private static readonly Dictionary<string, Bitmap> Sprites = new();
        private static readonly Dictionary<(string, Font), GraphicsPath> Outlines = new();

        static ScanKit()
        {
            Loc.Changed += (_, _) =>
            {
                foreach (Font font in CjkCache.Values)
                {
                    font.Dispose();
                }

                CjkCache.Clear();
                foreach (GraphicsPath path in Outlines.Values)
                {
                    path.Dispose();
                }

                Outlines.Clear();
            };
        }

        // ---- resources -------------------------------------------------------------------------

        public static Bitmap Sprite(string name)
        {
            if (!Sprites.TryGetValue(name, out Bitmap? bitmap))
            {
                using Stream stream = typeof(ScanKit).Assembly.GetManifestResourceStream($"Scan.{name}.png")
                    ?? throw new InvalidOperationException($"Missing scan asset {name}.png");

                // Copy out of the stream so the bitmap does not keep it open; 32bpp premultiplied draws fastest.
                using var decoded = new Bitmap(stream);
                bitmap = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.DrawImage(decoded, 0, 0, decoded.Width, decoded.Height);
                }

                Sprites[name] = bitmap;
            }

            return bitmap;
        }

        private static Font Roboto(float size, bool isMedium)
        {
            var key = ((int)Math.Round(size * 8), isMedium);
            if (!RobotoCache.TryGetValue(key, out Font? font))
            {
                FontFamily family = isMedium ? Ui.UiFonts.Medium : Ui.UiFonts.Regular;
                font = new Font(family, size, Ui.UiFonts.StyleFor(family), GraphicsUnit.Pixel);
                RobotoCache[key] = font;
            }

            return font;
        }

        /// <summary>Roboto has no Japanese or Chinese, so those strings use the language's own UI font.</summary>
        private static Font Cjk(float size, bool isMedium)
        {
            var key = ((int)Math.Round(size * 8), isMedium);
            if (!CjkCache.TryGetValue(key, out Font? font))
            {
                font = new Font(Loc.FontFamily, size, isMedium ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                CjkCache[key] = font;
            }

            return font;
        }

        private static bool NeedsCjk(string text)
        {
            foreach (char c in text)
            {
                if (c >= 0x2E80)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- text -----------------------------------------------------------------------------------

        /// <summary>
        /// Draws text on the spec's baseline. <paramref name="maxWidth"/> squeezes, then shrinks, text
        /// that would not fit - translations are often longer than the English of the mock-up.
        /// </summary>
        public static float Text(Graphics g, string text, TextSpec spec, TextAlign align = TextAlign.Left,
            Color? color = null, float maxWidth = 0f, TextSpec? style = null, float? x = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            TextSpec look = style ?? spec;
            bool cjk = NeedsCjk(text);
            float size = cjk ? look.Size * 0.94f : look.Size;
            float scaleX = cjk ? 1f : look.ScaleX;
            Font font = cjk ? Cjk(size, look.Medium) : Roboto(size, look.Medium);
            StringFormat format = StringFormat.GenericTypographic;

            float width = g.MeasureString(text, font, PointF.Empty, format).Width * scaleX;
            if (maxWidth > 0 && width > maxWidth)
            {
                float squeeze = Math.Max(0.78f, maxWidth / width);
                scaleX *= squeeze;
                width *= squeeze;
                if (width > maxWidth)
                {
                    size *= maxWidth / width;
                    font = cjk ? Cjk(size, look.Medium) : Roboto(size, look.Medium);
                    width = g.MeasureString(text, font, PointF.Empty, format).Width * scaleX;
                }
            }

            float left = x ?? align switch
            {
                TextAlign.Right => spec.Right - width,
                TextAlign.Center => spec.Center - width / 2f,
                _ => spec.X,
            };

            FontFamily family = font.FontFamily;
            float ascent = family.GetCellAscent(font.Style) * font.Size / family.GetEmHeight(font.Style);

            // Filled as outlines, unhinted like the mock-up's lettering, over the faint dark halo it sits in.
            GraphicsState state = g.Save();
            g.TranslateTransform(left, spec.Baseline - ascent);
            g.ScaleTransform(scaleX, 1f);
            GraphicsPath path = Outline(text, font, format);
            using (var halo = new Pen(Color.FromArgb(HaloAlpha, 0, 4, 10), size * 0.13f) { LineJoin = LineJoin.Round })
            {
                g.DrawPath(halo, path);
            }

            // A second fill a third of a pixel over gives the stems the mock-up's slightly heavier weight.
            using (var brush = new SolidBrush(color ?? look.Color))
            {
                g.FillPath(brush, path);
                g.TranslateTransform(0.3f, 0f);
                g.FillPath(brush, path);
            }

            g.Restore(state);
            return left + width;
        }

        /// <summary>Glyph outlines are cached: the screen repaints twenty times a second while scanning.</summary>
        private static GraphicsPath Outline(string text, Font font, StringFormat format)
        {
            var key = (text, font);
            if (!Outlines.TryGetValue(key, out GraphicsPath? path))
            {
                if (Outlines.Count > 1500)
                {
                    foreach (GraphicsPath old in Outlines.Values)
                    {
                        old.Dispose();
                    }

                    Outlines.Clear();
                }

                path = new GraphicsPath();
                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, PointF.Empty, format);
                Outlines[key] = path;
            }

            return path;
        }

        public static float Measure(Graphics g, string text, TextSpec spec)
        {
            bool cjk = NeedsCjk(text);
            Font font = cjk ? Cjk(spec.Size * 0.94f, spec.Medium) : Roboto(spec.Size, spec.Medium);
            return g.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic).Width * (cjk ? 1f : spec.ScaleX);
        }

        // ---- sprites ----------------------------------------------------------------------------------

        public static void Draw(Graphics g, string sprite, float x, float y)
        {
            Bitmap bitmap = Sprite(sprite);
            g.DrawImage(bitmap, new RectangleF(x, y, bitmap.Width, bitmap.Height));
        }

        public static void Draw(Graphics g, string sprite, RectangleF bounds)
        {
            g.DrawImage(Sprite(sprite), bounds);
        }

        /// <summary>
        /// Stretches a frame to a new size keeping its corners: corners are copied, edges stretched
        /// along their length and the middle filled. <paramref name="inset"/> trims the source first.
        /// </summary>
        public static void NineSlice(Graphics g, string sprite, RectangleF dest, float corner, float inset = 0f)
        {
            Bitmap bitmap = Sprite(sprite);
            var src = new RectangleF(inset, inset, bitmap.Width - inset * 2, bitmap.Height - inset * 2);
            float c = corner;

            float[] sx = { src.X, src.X + c, src.Right - c, src.Right };
            float[] sy = { src.Y, src.Y + c, src.Bottom - c, src.Bottom };
            float[] dx = { dest.X, dest.X + c, dest.Right - c, dest.Right };
            float[] dy = { dest.Y, dest.Y + c, dest.Bottom - c, dest.Bottom };

            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    var to = RectangleF.FromLTRB(dx[col], dy[row], dx[col + 1], dy[row + 1]);
                    if (to.Width <= 0 || to.Height <= 0)
                    {
                        continue;
                    }

                    // Overlap the pieces by a hair so no seam shows between them at fractional scales.
                    to.Inflate(0.3f, 0.3f);
                    g.DrawImage(bitmap,
                        new[] { new PointF(to.Left, to.Top), new PointF(to.Right, to.Top), new PointF(to.Left, to.Bottom) },
                        RectangleF.FromLTRB(sx[col], sy[row], sx[col + 1], sy[row + 1]),
                        GraphicsUnit.Pixel, attributes);
                }
            }
        }

        // ---- shapes -------------------------------------------------------------------------------------

        public static GraphicsPath Capsule(RectangleF r)
        {
            var path = new GraphicsPath();
            float d = Math.Min(r.Height, r.Width);
            if (r.Width <= d)
            {
                path.AddEllipse(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }

        public static void FillCapsule(Graphics g, Brush brush, RectangleF r)
        {
            using GraphicsPath path = Capsule(r);
            g.FillPath(brush, path);
        }

        public static void FillCapsule(Graphics g, Color color, RectangleF r)
        {
            using var brush = new SolidBrush(color);
            FillCapsule(g, brush, r);
        }

        /// <summary>Soft light around a shape, built from a few widening translucent capsules.</summary>
        public static void Glow(Graphics g, RectangleF r, Color color, float reach, int strength)
        {
            for (int i = 4; i >= 1; i--)
            {
                float grow = reach * i / 4f;
                int alpha = Math.Clamp(strength / (i + 1), 1, 255);
                FillCapsule(g, Color.FromArgb(alpha, color), RectangleF.Inflate(r, grow, grow * 0.8f));
            }
        }

        public static void Dot(Graphics g, PointF centre, float radius, Color color, bool glow)
        {
            if (glow)
            {
                for (int i = 3; i >= 1; i--)
                {
                    float r = radius + i * 2f;
                    using var halo = new SolidBrush(Color.FromArgb(28 / i + 8, color));
                    g.FillEllipse(halo, centre.X - r, centre.Y - r, r * 2, r * 2);
                }
            }

            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, centre.X - radius, centre.Y - radius, radius * 2, radius * 2);
        }
    }
}

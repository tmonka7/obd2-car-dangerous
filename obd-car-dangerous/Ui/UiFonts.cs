using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace obd_car_dangerous.Ui
{
    /// <summary>
    /// The Roboto faces the Redline design is set in, embedded so the app looks the same on any PC.
    /// Roboto has no Japanese or Chinese; callers use the language's own font for those.
    /// </summary>
    internal static class UiFonts
    {
        private static readonly PrivateFontCollection Collection = new();
        private static FontFamily? regular;
        private static FontFamily? medium;

        public static FontFamily Regular
        {
            get
            {
                Load();
                return regular!;
            }
        }

        /// <summary>Roboto Medium: what the design uses wherever the app used to ask for bold.</summary>
        public static FontFamily Medium
        {
            get
            {
                Load();
                return medium!;
            }
        }

        /// <summary>Style to pass with a family: Medium is its own family, drawn with the regular style.</summary>
        public static FontStyle StyleFor(FontFamily family) =>
            family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;

        private static void Load()
        {
            if (regular is not null)
            {
                return;
            }

            foreach (string file in new[] { "Roboto-Regular", "Roboto-Medium" })
            {
                using Stream stream = typeof(UiFonts).Assembly.GetManifestResourceStream($"Fonts.{file}.ttf")
                    ?? throw new InvalidOperationException($"Missing font {file}.ttf");
                byte[] data = new byte[stream.Length];
                stream.ReadExactly(data);

                // GDI+ reads the font from this memory for as long as the process runs, so it is never freed.
                IntPtr memory = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, memory, data.Length);
                Collection.AddMemoryFont(memory, data.Length);
            }

            regular = Collection.Families.FirstOrDefault(f => f.Name == "Roboto") ?? Collection.Families[0];
            medium = Collection.Families.FirstOrDefault(f => f.Name == "Roboto Medium") ?? regular;
        }
    }
}

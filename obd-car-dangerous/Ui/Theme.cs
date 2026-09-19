namespace obd_car_dangerous.Ui
{
    /// <summary>Colour palette for the whole app. Switching Dark changes every page.</summary>
    internal static class Theme
    {
        public static event EventHandler? Changed;

        private static bool dark;

        public static bool Dark
        {
            get => dark;
            set
            {
                if (dark == value)
                {
                    return;
                }

                dark = value;
                Changed?.Invoke(null, EventArgs.Empty);
            }
        }

        // Brand colours - elevated to feel more premium and modern.
        public static Color Accent => Color.FromArgb(66, 133, 244);
        public static Color AccentDeep => Color.FromArgb(23, 86, 196);
        public static Color Good => Color.FromArgb(38, 194, 114);
        public static Color Warn => Color.FromArgb(255, 178, 60);
        public static Color Critical => Color.FromArgb(235, 77, 96);
        public static Color Violet => Color.FromArgb(128, 98, 255);
        public static Color Orange => Color.FromArgb(255, 153, 72);

        // Shell (sidebar + header) keeps the deep blue brand but with a richer finish.
        public static Color ShellTop => Color.FromArgb(15, 69, 146);
        public static Color ShellBottom => Color.FromArgb(8, 43, 98);
        public static Color ShellItem => Color.FromArgb(22, 90, 190);
        public static Color ShellText => Color.FromArgb(233, 242, 255);

        public static Color PageTop => dark ? Color.FromArgb(6, 17, 31) : Color.FromArgb(244, 248, 254);
        public static Color PageBottom => dark ? Color.FromArgb(11, 23, 39) : Color.FromArgb(234, 240, 249);
        public static Color Card => dark ? Color.FromArgb(18, 38, 62) : Color.FromArgb(255, 255, 255);
        public static Color CardAlt => dark ? Color.FromArgb(23, 48, 78) : Color.FromArgb(247, 250, 255);
        public static Color Border => dark ? Color.FromArgb(46, 78, 112) : Color.FromArgb(215, 225, 238);
        public static Color Text => dark ? Color.FromArgb(239, 245, 255) : Color.FromArgb(11, 31, 56);
        public static Color TextSoft => dark ? Color.FromArgb(151, 179, 211) : Color.FromArgb(99, 118, 145);
        public static Color Shadow => dark ? Color.FromArgb(80, 0, 0, 0) : Color.FromArgb(30, 27, 62, 110);

        public static Color Severity(string severity) => severity switch
        {
            "High" or "Critical" => Critical,
            "Medium" or "Warning" => Warn,
            _ => Good,
        };

        public static void Toggle() => Dark = !Dark;
    }
}

namespace obd_car_dangerous.Ui
{
    /// <summary>
    /// Colour palette for the whole app: the Redline look of the Full System Scan design - near-black
    /// navy panels, red for the brand and the selection, cyan-blue for information. The design has a
    /// single dark palette, so there is no light theme.
    /// </summary>
    internal static class Theme
    {
<<<<<<< HEAD
        // Brand colours.
        public static Color Accent => Color.FromArgb(238, 24, 52);
        public static Color AccentDeep => Color.FromArgb(150, 10, 32);
        public static Color Info => Color.FromArgb(56, 178, 240);
        public static Color Good => Color.FromArgb(18, 206, 134);
        public static Color Warn => Color.FromArgb(247, 176, 40);
        public static Color Critical => Color.FromArgb(244, 44, 66);
        public static Color Violet => Color.FromArgb(52, 142, 240);
        public static Color Orange => Color.FromArgb(247, 146, 20);

        // Shell: rail and header.
        public static Color ShellTop => Color.FromArgb(7, 14, 25);
        public static Color ShellBottom => Color.FromArgb(3, 8, 15);
        public static Color ShellItem => Color.FromArgb(200, 12, 38);
        public static Color ShellText => Color.FromArgb(200, 213, 230);
        public static Color ShellLine => Color.FromArgb(24, 34, 48);

        public static Color PageTop => Color.FromArgb(5, 11, 20);
        public static Color PageBottom => Color.FromArgb(2, 6, 12);
        public static Color Card => Color.FromArgb(10, 17, 28);
        public static Color CardAlt => Color.FromArgb(16, 25, 39);
        public static Color Border => Color.FromArgb(36, 49, 68);
        public static Color Text => Color.FromArgb(236, 241, 247);
        public static Color TextSoft => Color.FromArgb(146, 170, 199);
        public static Color Shadow => Color.FromArgb(110, 0, 0, 0);

        /// <summary>Empty part of bars and gauges.</summary>
        public static Color Track => Color.FromArgb(24, 34, 50);
=======
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
>>>>>>> fa2d1446b918600c417ab017b70bedec48b59b5a

        public static Color Severity(string severity) => severity switch
        {
            "High" or "Critical" => Critical,
            "Medium" or "Warning" => Warn,
            _ => Good,
        };
    }
}

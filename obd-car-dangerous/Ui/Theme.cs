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

        // Brand colours - identical in both themes.
        public static Color Accent => Color.FromArgb(0, 122, 255);
        public static Color AccentDeep => Color.FromArgb(9, 71, 150);
        public static Color Good => Color.FromArgb(29, 185, 84);
        public static Color Warn => Color.FromArgb(247, 181, 0);
        public static Color Critical => Color.FromArgb(232, 32, 52);
        public static Color Violet => Color.FromArgb(99, 91, 255);
        public static Color Orange => Color.FromArgb(247, 146, 20);

        // Shell (sidebar + header) is always the deep blue of the mock-ups.
        public static Color ShellTop => Color.FromArgb(10, 63, 138);
        public static Color ShellBottom => Color.FromArgb(6, 40, 92);
        public static Color ShellItem => Color.FromArgb(19, 84, 170);
        public static Color ShellText => Color.FromArgb(226, 238, 252);

        public static Color PageTop => dark ? Color.FromArgb(9, 24, 44) : Color.FromArgb(238, 244, 252);
        public static Color PageBottom => dark ? Color.FromArgb(5, 14, 28) : Color.FromArgb(222, 233, 247);
        public static Color Card => dark ? Color.FromArgb(17, 38, 66) : Color.White;
        public static Color CardAlt => dark ? Color.FromArgb(22, 48, 82) : Color.FromArgb(244, 248, 253);
        public static Color Border => dark ? Color.FromArgb(38, 72, 114) : Color.FromArgb(216, 227, 240);
        public static Color Text => dark ? Color.FromArgb(233, 241, 252) : Color.FromArgb(14, 33, 61);
        public static Color TextSoft => dark ? Color.FromArgb(150, 176, 210) : Color.FromArgb(101, 122, 150);
        public static Color Shadow => dark ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(28, 20, 50, 90);

        public static Color Severity(string severity) => severity switch
        {
            "High" or "Critical" => Critical,
            "Medium" or "Warning" => Warn,
            _ => Good,
        };

        public static void Toggle() => Dark = !Dark;
    }
}

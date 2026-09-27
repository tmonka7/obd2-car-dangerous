namespace obd_car_dangerous.Ui
{
    /// <summary>
    /// Colour palette for the whole app: the Redline look of the Full System Scan design - near-black
    /// navy panels, red for the brand and the selection, cyan-blue for information. The design has a
    /// single dark palette, so there is no light theme.
    /// </summary>
    internal static class Theme
    {
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

        public static Color Severity(string severity) => severity switch
        {
            "High" or "Critical" => Critical,
            "Medium" or "Warning" => Warn,
            _ => Good,
        };
    }
}

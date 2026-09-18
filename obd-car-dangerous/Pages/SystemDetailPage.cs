using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Detail for one vehicle system: status, its fault codes and its live readings.</summary>
    internal sealed class SystemDetailPage : PageBase
    {
        private string systemName = "Engine";

        public override string Title => Loc.T("sysdetail.title", Loc.SystemName(systemName));

        public override bool ShowBack => true;

        public override void OnEnter(object? argument)
        {
            if (argument is string name)
            {
                systemName = name;
            }

            ScrollY = 0;
        }

        private SystemHealth Health =>
            AppState.Systems.FirstOrDefault(s => s.Name == systemName) ?? AppState.Systems[0];

        private static string[] PidsFor(string system) => system switch
        {
            "Engine" => new[] { "rpm", "load", "coolant", "throttle", "timing", "oil" },
            "Transmission" => new[] { "speed", "rpm", "load", "oil" },
            "ABS" => new[] { "speed", "rpm" },
            "Airbag" => new[] { "battery" },
            "Battery" => new[] { "battery", "rpm" },
            _ => new[] { "rpm", "speed" },
        };

        private static string DtcSystem(string system) => system == "Battery" ? "Body" : system;

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);

            SystemHealth health = Health;
            Color color = AppState.StatusColor(health.Status);

            var hero = new RectangleF(pad, top + 18, W - pad * 2, 150);
            Draw.Card(g, hero, 20f);

            var iconBox = new RectangleF(hero.X + 26, hero.Y + 30, 90, 90);
            Draw.FillRounded(g, color, iconBox, 20f);
            Icons.Draw(g, health.Icon, RectangleF.Inflate(iconBox, -22, -22), Color.White, color);

            Draw.Text(g, Loc.SystemName(health.Name), Draw.Font(34, FontStyle.Bold), Theme.Text, iconBox.Right + 24, hero.Y + 30);
            Draw.Text(g, health.Detail, Draw.Font(20), Theme.TextSoft, iconBox.Right + 24, hero.Y + 78);

            var pill = new RectangleF(hero.Right - 210, hero.Y + 34, 160, 46);
            Draw.Pill(g, pill, Draw.Alpha(color, 42), Loc.StatusWord(health.Status), Draw.Font(22, FontStyle.Bold), color);
            Draw.TextIn(g, Loc.T("sysdetail.score", health.Score), Draw.Font(17), Theme.TextSoft,
                new RectangleF(hero.Right - 300, pill.Bottom + 6, 250, 28), StringAlignment.Far, StringAlignment.Center, false);

            float contentTop = hero.Bottom + 18;
            float contentH = H - contentTop - pad;
            float leftW = (W - pad * 3) * 0.48f;

            DrawCodes(g, new RectangleF(pad, contentTop, leftW, contentH));
            DrawReadings(g, new RectangleF(pad * 2 + leftW, contentTop, W - leftW - pad * 3, contentH));
        }

        private void DrawCodes(Graphics g, RectangleF bounds)
        {
            Draw.Card(g, bounds, 18f);
            Draw.Text(g, Loc.T("sysdetail.codes"), Draw.Font(22, FontStyle.Bold), Theme.Text, bounds.X + 20, bounds.Y + 16);

            List<DtcRecord> codes = AppState.Dtc.All
                .Where(c => c.System == DtcSystem(systemName))
                .OrderBy(c => c.Status)
                .ThenByDescending(c => c.DetectedAt)
                .ToList();

            if (codes.Count == 0)
            {
                Draw.TextCentered(g, Loc.T("sysdetail.nocodes"), Draw.Font(20), Theme.TextSoft,
                    new RectangleF(bounds.X, bounds.Y + 60, bounds.Width, bounds.Height - 80));
                return;
            }

            float y = bounds.Y + 58;
            foreach (DtcRecord code in codes)
            {
                if (y + 76 > bounds.Bottom - 10)
                {
                    break;
                }

                var row = new RectangleF(bounds.X + 14, y, bounds.Width - 28, 70);
                string id = $"sysdetail-{code.Code}-{code.Status}";
                Color severity = Theme.Severity(code.Severity);
                Color statusColor = code.Status == DtcStatus.History ? Theme.TextSoft : severity;

                Draw.FillRounded(g, IsHover(id) ? Theme.CardAlt : Draw.Alpha(statusColor, 18), row, 12f);
                Draw.FillRounded(g, statusColor, new RectangleF(row.X + 10, row.Y + 14, 6, row.Height - 28), 3f);

                Draw.TextIn(g, code.Code, Draw.Font(22, FontStyle.Bold), Theme.Text,
                    new RectangleF(row.X + 28, row.Y, 110, row.Height), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, code.Description, Draw.Font(18), Theme.TextSoft,
                    new RectangleF(row.X + 140, row.Y + 8, row.Width - 240, row.Height - 16), StringAlignment.Near, StringAlignment.Center);
                Draw.TextIn(g, Loc.Status(code.Status), Draw.Font(16, FontStyle.Bold), statusColor,
                    new RectangleF(row.Right - 110, row.Y, 90, row.Height), StringAlignment.Far, StringAlignment.Center, false);

                Hit(row, () => Shell.Navigate("dtcdetail", code), id);
                y += 78;
            }
        }

        private void DrawReadings(Graphics g, RectangleF bounds)
        {
            Draw.Card(g, bounds, 18f);
            Draw.Text(g, Loc.T("sysdetail.readings"), Draw.Font(22, FontStyle.Bold), Theme.Text, bounds.X + 20, bounds.Y + 16);

            string[] keys = PidsFor(systemName);
            float y = bounds.Y + 58;
            float rowH = Math.Min(74f, (bounds.Height - 80) / keys.Length);

            foreach (string key in keys)
            {
                Pid pid = Telemetry.Find(key);
                float value = pid.Read(AppState.Telemetry);
                float fraction = Math.Clamp((value - pid.Min) / Math.Max(0.0001f, pid.Max - pid.Min), 0f, 1f);
                var row = new RectangleF(bounds.X + 20, y, bounds.Width - 40, rowH);
                string id = $"sysread-{key}";

                Draw.TextIn(g, Loc.Pid(pid.Key, pid.Label), Draw.Font(19), Theme.TextSoft,
                    new RectangleF(row.X, row.Y, row.Width * 0.5f, rowH * 0.55f), StringAlignment.Near, StringAlignment.Center, false);
                Draw.TextIn(g, $"{value.ToString(pid.Format)} {pid.Unit}", Draw.Font(21, FontStyle.Bold), Theme.Text,
                    new RectangleF(row.X + row.Width * 0.5f, row.Y, row.Width * 0.5f, rowH * 0.55f), StringAlignment.Far, StringAlignment.Center, false);

                var bar = new RectangleF(row.X, row.Y + rowH * 0.62f, row.Width, 9);
                Draw.FillRounded(g, Theme.Dark ? Color.FromArgb(34, 62, 98) : Color.FromArgb(232, 238, 246), bar, 4.5f);
                Draw.FillRounded(g, fraction > 0.85f ? Theme.Warn : Theme.Accent,
                    new RectangleF(bar.X, bar.Y, Math.Max(8f, bar.Width * fraction), bar.Height), 4.5f);

                Hit(row, () => Shell.Navigate("graph", key), id);
                y += rowH;
            }
        }
    }
}

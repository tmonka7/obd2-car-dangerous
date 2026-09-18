using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Live PID values grouped into tabs, with a chart of the selected parameter.</summary>
    internal sealed class LiveDataPage : PageBase
    {
        private int tab;
        private string selectedPid = "rpm";

        public override string Title => "Live Data";

        public override bool ShowBack => true;

        public override void OnEnter(object? argument)
        {
            if (argument is string group)
            {
                int index = Telemetry.Groups.ToList().IndexOf(group);
                if (index >= 0)
                {
                    tab = index;
                    selectedPid = PidsForTab()[0].Key;
                }
            }
        }

        private IReadOnlyList<Pid> PidsForTab() =>
            Telemetry.Pids.Where(p => p.Group == Telemetry.Groups[tab]).ToList();

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g, AppState.Connection.IsConnected ? "streaming" : "adapter offline");

            var tabsRect = new RectangleF(pad, top + 18, W - pad * 2, 58);
            DrawTabs(g, tabsRect, Telemetry.Groups, tab, index =>
            {
                tab = index;
                selectedPid = PidsForTab().First().Key;
                Invalidate();
            }, "live-tab");

            IReadOnlyList<Pid> pids = PidsForTab();
            if (!pids.Any(p => p.Key == selectedPid))
            {
                selectedPid = pids[0].Key;
            }

            float contentTop = tabsRect.Bottom + 20;
            float contentH = H - contentTop - pad;
            float leftW = (W - pad * 3) * 0.5f;

            DrawGauges(g, new RectangleF(pad, contentTop, leftW, contentH), pids);
            DrawChart(g, new RectangleF(pad * 2 + leftW, contentTop, W - leftW - pad * 3, contentH));
        }

        private void DrawGauges(Graphics g, RectangleF bounds, IReadOnlyList<Pid> pids)
        {
            const int columns = 2;
            int rows = (int)Math.Ceiling(pids.Count / (float)columns);
            float gap = 16f;
            float cellW = (bounds.Width - gap * (columns - 1)) / columns;
            float cellH = (bounds.Height - gap * (rows - 1)) / rows;

            for (int i = 0; i < pids.Count; i++)
            {
                Pid pid = pids[i];
                int row = i / columns;
                int column = i % columns;
                var rect = new RectangleF(bounds.X + column * (cellW + gap), bounds.Y + row * (cellH + gap), cellW, cellH);
                bool active = pid.Key == selectedPid;
                string id = $"gauge-{pid.Key}";

                Draw.Card(g, rect, 18f, IsHover(id) ? Theme.CardAlt : Theme.Card);
                if (active)
                {
                    Draw.StrokeRounded(g, Theme.Accent, rect, 18f, 2.5f);
                }

                Draw.TextIn(g, pid.Label, Draw.Font(20, FontStyle.Bold), Theme.TextSoft,
                    new RectangleF(rect.X + 18, rect.Y + 12, rect.Width - 36, 28), StringAlignment.Near, StringAlignment.Center, false);

                float value = pid.Read(AppState.Telemetry);
                float fraction = (value - pid.Min) / Math.Max(0.0001f, pid.Max - pid.Min);
                Color color = ValueColor(pid, fraction);

                // The arc only uses the top half of its box, so the dial plus unit is size/2 + 28 tall.
                float top = rect.Y + 48;
                float available = rect.Bottom - 14 - top;
                float size = Math.Min(rect.Width * 0.56f, (available - 28f) * 2f);
                float blockTop = top + (available - (size / 2f + 28f)) / 2f;
                var gaugeBox = new RectangleF(rect.X + 20, blockTop, size, size);

                Draw.ArcGauge(g, gaugeBox, fraction, color, Math.Max(8f, size * 0.1f));

                var valueBox = new RectangleF(gaugeBox.X, blockTop + size * 0.16f, size, size * 0.28f);
                string text = value.ToString(pid.Format);
                float fontSize = Math.Min(38f, size * 0.25f);
                while (fontSize > 15f && Draw.Measure(g, text, Draw.Font(fontSize, FontStyle.Bold)).Width > size * 0.6f)
                {
                    fontSize -= 2f;
                }

                Draw.TextCentered(g, text, Draw.Font(fontSize, FontStyle.Bold), Theme.Text, valueBox);
                Draw.TextCentered(g, pid.Unit, Draw.Font(17), Theme.TextSoft,
                    new RectangleF(gaugeBox.X, blockTop + size * 0.5f + 2, size, 26));

                // Sparkline to the right of the dial.
                float[] recent = Charts.Downsample(AppState.Telemetry.HistoryOf(pid.Key).Recent(150), 40);
                var spark = new RectangleF(gaugeBox.Right + 16, rect.Y + 56, rect.Right - gaugeBox.Right - 36, rect.Height - 86);
                if (spark.Width > 70 && spark.Height > 30 && recent.Length > 2)
                {
                    DrawSparkline(g, spark, recent, color);
                }

                string key = pid.Key;
                Hit(rect, () =>
                {
                    selectedPid = key;
                    Invalidate();
                }, id);
            }
        }

        private static void DrawSparkline(Graphics g, RectangleF bounds, float[] values, Color color)
        {
            float min = values.Min();
            float max = values.Max();
            float span = Math.Max(max - min, Math.Abs(max) * 0.05f + 0.001f);
            min -= span * 0.2f;
            max += span * 0.2f;

            var points = new PointF[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float t = (values[i] - min) / (max - min);
                points[i] = new PointF(
                    bounds.X + bounds.Width * i / (values.Length - 1),
                    bounds.Bottom - bounds.Height * Math.Clamp(t, 0f, 1f));
            }

            using var pen = new Pen(Draw.Alpha(color, 200), 2.4f)
            {
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };
            g.DrawLines(pen, points);
        }

        private static Color ValueColor(Pid pid, float fraction)
        {
            bool hot = pid.Key switch
            {
                "coolant" => pid.Read(AppState.Telemetry) > AppState.Settings.CoolantLimit - 15,
                "rpm" => pid.Read(AppState.Telemetry) > AppState.Settings.RpmLimit - 500,
                "speed" => pid.Read(AppState.Telemetry) > AppState.Settings.SpeedLimit - 10,
                _ => false,
            };

            if (hot)
            {
                return Theme.Critical;
            }

            return fraction > 0.85f ? Theme.Warn : Theme.Good;
        }

        private void DrawChart(Graphics g, RectangleF bounds)
        {
            Draw.Card(g, bounds, 18f);

            Pid pid = Telemetry.Find(selectedPid);
            float[] values = Charts.Downsample(AppState.Telemetry.HistoryOf(pid.Key).Recent(60_000 / Telemetry.TickMs), 120);

            float min = pid.Min;
            float max = pid.Max;
            if (values.Length > 2)
            {
                float lo = values.Min();
                float hi = values.Max();
                float span = Math.Max(hi - lo, (pid.Max - pid.Min) * 0.12f);
                min = Math.Max(pid.Min, lo - span * 0.25f);
                max = Math.Min(pid.Max, hi + span * 0.25f);
                if (max - min < 0.001f)
                {
                    max = min + 1;
                }
            }

            Charts.Line(g, RectangleF.Inflate(bounds, -10, -10), values, min, max, Theme.Accent,
                $"{pid.Label} ({pid.Unit})", new[] { "0s", "10s", "20s", "30s", "40s", "50s", "60s" });

            var expand = new RectangleF(bounds.Right - 190, bounds.Y + 14, 170, 40);
            DrawGhostButton(g, expand, "Full graph", Theme.Accent, () => Shell.Navigate("graph", selectedPid), "live-expand", 12f);
        }
    }
}

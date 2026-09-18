using System.Drawing.Drawing2D;
using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>Full width recorder for one parameter over 1, 5 or 10 minutes.</summary>
    internal sealed class LiveGraphPage : PageBase
    {
        private static readonly string[] Quick = { "rpm", "coolant", "o2", "stft", "speed", "maf" };
        private static readonly (int Minutes, int Seconds)[] Ranges = { (1, 60), (5, 300), (10, 600) };

        private string pidKey = "rpm";
        private int range;

        public override string Title => Loc.T("graph.title");

        public override bool ShowBack => true;

        public override void OnEnter(object? argument)
        {
            if (argument is string key && Telemetry.Pids.Any(p => p.Key == key))
            {
                pidKey = key;
            }
        }

        protected override void Render(Graphics g)
        {
            const float pad = 24f;
            float top = DrawHeader(g);

            // Parameter chips.
            var chipRow = new RectangleF(pad, top + 16, W - pad * 2, 54);
            float gap = 12f;
            float chipW = (chipRow.Width - gap * (Quick.Length - 1)) / Quick.Length;
            for (int i = 0; i < Quick.Length; i++)
            {
                Pid pid = Telemetry.Find(Quick[i]);
                var rect = new RectangleF(chipRow.X + i * (chipW + gap), chipRow.Y, chipW, chipRow.Height);
                bool active = pid.Key == pidKey;
                string id = $"graph-pid-{pid.Key}";

                Draw.FillRounded(g, Hovered(active ? Theme.Accent : Theme.Card, id), rect, 14f);
                if (!active)
                {
                    Draw.StrokeRounded(g, Theme.Border, rect, 14f, 1f);
                }

                Draw.TextCentered(g, Loc.Pid(pid.Key, pid.Label), Draw.Font(20, FontStyle.Bold), active ? Color.White : Theme.TextSoft, rect);

                string key = pid.Key;
                Hit(rect, () =>
                {
                    pidKey = key;
                    Invalidate();
                }, id);
            }

            // Dark plot panel, as in the design.
            var panel = new RectangleF(pad, chipRow.Bottom + 18, W - pad * 2, H - chipRow.Bottom - 18 - 96 - pad);
            Draw.CardShadow(g, panel, 20f);
            Draw.GradientRounded(g, Color.FromArgb(14, 52, 102), Color.FromArgb(7, 28, 60), panel, 20f);

            Pid selected = Telemetry.Find(pidKey);
            int samples = Ranges[range].Seconds * 1000 / Telemetry.TickMs;
            float[] values = Charts.Downsample(AppState.Telemetry.HistoryOf(pidKey).Recent(samples), 200);

            float min = selected.Min;
            float max = selected.Max;
            if (values.Length > 2)
            {
                float lo = values.Min();
                float hi = values.Max();
                float span = Math.Max(hi - lo, (selected.Max - selected.Min) * 0.1f);
                min = Math.Max(selected.Min, lo - span * 0.2f);
                max = Math.Min(selected.Max, hi + span * 0.2f);
                if (max - min < 0.001f)
                {
                    max = min + 1;
                }
            }

            string[] labels = BuildTimeLabels(Ranges[range].Seconds);
            Charts.Line(g, RectangleF.Inflate(panel, -12, -12), values, min, max,
                Color.FromArgb(46, 230, 120), $"{Loc.Pid(selected.Key, selected.Label)} ({selected.Unit})", labels, darkPlot: true);

            DrawStats(g, panel, values, selected);

            // Range buttons.
            var rangeRow = new RectangleF(pad, panel.Bottom + 16, W - pad * 2, 64);
            float rangeW = (rangeRow.Width - gap * (Ranges.Length - 1)) / Ranges.Length;
            for (int i = 0; i < Ranges.Length; i++)
            {
                var rect = new RectangleF(rangeRow.X + i * (rangeW + gap), rangeRow.Y, rangeW, rangeRow.Height);
                bool active = i == range;
                string id = $"graph-range-{i}";
                Draw.FillRounded(g, Hovered(active ? Theme.Accent : Theme.Card, id), rect, 16f);
                if (!active)
                {
                    Draw.StrokeRounded(g, Theme.Border, rect, 16f, 1f);
                }

                Draw.TextCentered(g, Loc.T("graph.range", Ranges[i].Minutes), Draw.Font(24, FontStyle.Bold), active ? Color.White : Theme.TextSoft, rect);

                int index = i;
                Hit(rect, () =>
                {
                    range = index;
                    Invalidate();
                }, id);
            }
        }

        private static string[] BuildTimeLabels(int seconds)
        {
            var labels = new string[7];
            for (int i = 0; i < labels.Length; i++)
            {
                int value = seconds * i / (labels.Length - 1);
                labels[i] = seconds <= 60 ? $"{value}s" : $"{value / 60f:0.#}m";
            }

            return labels;
        }

        private static void DrawStats(Graphics g, RectangleF panel, float[] values, Pid pid)
        {
            if (values.Length == 0)
            {
                return;
            }

            (string Label, float Value)[] stats =
            {
                (Loc.T("graph.now"), values[^1]),
                (Loc.T("graph.min"), values.Min()),
                (Loc.T("graph.max"), values.Max()),
                (Loc.T("graph.avg"), values.Average()),
            };

            float w = 130f;
            float x = panel.Right - 24 - w * stats.Length;
            foreach ((string label, float value) in stats)
            {
                Draw.TextIn(g, label, Draw.Font(16), Color.FromArgb(150, 185, 220),
                    new RectangleF(x, panel.Y + 12, w, 22), StringAlignment.Center, StringAlignment.Center, false);
                Draw.TextIn(g, value.ToString(pid.Format), Draw.Font(24, FontStyle.Bold), Color.White,
                    new RectangleF(x, panel.Y + 32, w, 30), StringAlignment.Center, StringAlignment.Center, false);
                x += w;
            }
        }
    }
}

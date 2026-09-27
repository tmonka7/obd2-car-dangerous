using System.Drawing.Drawing2D;
using obd_car_dangerous.Pages.Scan;
using obd_car_dangerous.Services;
using obd_car_dangerous.Ui;

namespace obd_car_dangerous.Pages
{
    /// <summary>
    /// Full System Scan, built to match design/full-system-scan.png. The screen is the mock-up itself:
    /// a raster plate with the text and moving parts painted out (tools/scan_assets.py), frames and icons
    /// cut from the same picture, and live text set on the mock-up's own baselines. It brings its own
    /// navigation rail, so the shell hides the app's rail while it is showing.
    /// </summary>
    internal sealed class FullScanPage : PageBase
    {
        // The tablet screen inside the mock-up; every coordinate below is a mock-up pixel.
        private const float ScreenX = 38f;
        private const float ScreenY = 36f;
        private const float ScreenW = 1464f;
        private const float ScreenH = 950f;

        private static readonly (string Key, string Nav, object? Argument, float Y)[] NavRows =
        {
            ("scan.nav.home", "home", null, 148),
            ("scan.nav.diagnose", "diagnostics", null, 209),
            ("scan.nav.live", "livedata", null, 273),
            ("scan.nav.vehicle", "settings", "vehicle", 339),
            ("scan.nav.reports", "dtc", null, 406),
            ("scan.nav.history", "alarms", null, 473),
            ("scan.nav.garage", "settings", "connection", 540),
            ("scan.nav.settings", "settings", null, 607),
        };

        private static readonly TextSpec[] NavSpecs =
        {
            ScanText.NavHome, ScanText.NavDiagnose, ScanText.NavLive, ScanText.NavVehicle,
            ScanText.NavReports, ScanText.NavHistory, ScanText.NavGarage, ScanText.NavSettings,
        };

        // Callouts on the car: frame and the state the mock-up draws it in.
        private static readonly (string Key, RectangleF Frame, ModuleState Drawn, PointF Icon, TextSpec Title, TextSpec Status)[] Callouts =
        {
            ("ecm", RectangleF.FromLTRB(260, 252, 477, 307), ModuleState.Passed, ScanText.CalloutIconEcm, ScanText.CalloutEcmTitle, ScanText.CalloutEcmStatus),
            ("bcm", RectangleF.FromLTRB(572, 204, 766, 258), ModuleState.Pending, ScanText.CalloutIconBcm, ScanText.CalloutBcmTitle, ScanText.CalloutBcmStatus),
            ("tcm", RectangleF.FromLTRB(888, 209, 1122, 263), ModuleState.Passed, ScanText.CalloutIconTcm, ScanText.CalloutTcmTitle, ScanText.CalloutTcmStatus),
            ("srs", RectangleF.FromLTRB(902, 604, 1041, 658), ModuleState.Pending, ScanText.CalloutIconSrs, ScanText.CalloutSrsTitle, ScanText.CalloutSrsStatus),
            ("abs", RectangleF.FromLTRB(713, 643, 853, 697), ModuleState.Scanning, ScanText.CalloutIconAbs, ScanText.CalloutAbsTitle, ScanText.CalloutAbsStatus),
        };

        // Card slots along the bottom, with the module and state the mock-up draws in each.
        private static readonly (string Key, float X0, float X1, ModuleState Drawn, TextSpec Title, TextSpec Sub, TextSpec Status, TextSpec Value)[] Slots =
        {
            ("ecm", 227, 394, ModuleState.Passed, ScanText.CardEcmTitle, ScanText.CardEcmSub, ScanText.CardEcmStatus, ScanText.CardEcmValue),
            ("tcm", 408, 582, ModuleState.Passed, ScanText.CardTcmTitle, ScanText.CardTcmSub, ScanText.CardTcmStatus, ScanText.CardTcmValue),
            ("abs", 595, 792, ModuleState.Scanning, ScanText.CardAbsTitle, ScanText.CardAbsSub, ScanText.CardAbsStatus, ScanText.CardAbsValue),
            ("srs", 805, 952, ModuleState.Pending, ScanText.CardSrsTitle, ScanText.CardSrsSub, ScanText.CardSrsStatus, ScanText.CardSrsValue),
            ("bcm", 965, 1123, ModuleState.Pending, ScanText.CardBcmTitle, ScanText.CardBcmSub, ScanText.CardBcmStatus, ScanText.CardBcmValue),
            ("tpms", 1136, 1299, ModuleState.Pending, ScanText.CardTpmsTitle, ScanText.CardTpmsSub, ScanText.CardTpmsStatus, ScanText.CardTpmsValue),
            ("hvac", 1312, 1459, ModuleState.Pending, ScanText.CardHvacTitle, ScanText.CardHvacSub, ScanText.CardHvacStatus, ScanText.CardHvacValue),
        };

        private const float CardTop = 844f;
        private const float CardBottom = 951f;
        private const float FrameMargin = 10f;

        private static readonly RectangleF[] LanguageCells =
        {
            RectangleF.FromLTRB(1156, 52, 1200, 86),
            RectangleF.FromLTRB(1202, 52, 1258, 86),
            RectangleF.FromLTRB(1260, 52, 1313, 86),
        };

        private Bitmap? plate;
        private Bitmap? layer;
        private string layerKey = string.Empty;

        /// <summary>
        /// True while painting the cached layer: everything that only changes with the scan state. The
        /// pass on screen then copies that layer and adds just the moving parts and the click regions.
        /// </summary>
        private bool layerPass;
        private int cardPage;
        private float k = 1f;
        private float ox;
        private float oy;

        public FullScanPage()
        {
            ScanSession.Changed += OnScanChanged;
        }

        public override string Title => Loc.T("scan.title");

        public override bool ShowBack => true;

        public override void OnEnter(object? argument)
        {
            if (argument is "preview")
            {
                // The moment the mock-up captures: ABS at 72 %, 14 of 21 modules done.
                ScanSession.Preview("abs", 0.72f, TimeSpan.FromSeconds(154), TimeSpan.FromSeconds(18));
                cardPage = 0;
            }
            else if (argument is "preview-done")
            {
                ScanSession.PreviewFinished();
                cardPage = 0;
            }
            else if (argument is "restart" || (!ScanSession.Running && !ScanSession.Finished))
            {
                ScanSession.Start();
                cardPage = 0;
            }
        }

        private void OnScanChanged(object? sender, EventArgs e)
        {
            if (Visible)
            {
                Invalidate();
            }
        }

        // ---- coordinates ------------------------------------------------------------------------

        private RectangleF Map(RectangleF r) =>
            new(ox + (r.X - ScreenX) * k, oy + (r.Y - ScreenY) * k, r.Width * k, r.Height * k);

        private void HitAt(RectangleF mock, Action onClick, string id)
        {
            if (!layerPass)
            {
                Hit(Map(mock), onClick, id);
            }
        }

        /// <summary>A frame's rectangle as its sprite was cut: the margin around it, last row and column included.</summary>
        private static RectangleF Outer(RectangleF frame) =>
            RectangleF.FromLTRB(frame.Left - FrameMargin, frame.Top - FrameMargin, frame.Right + FrameMargin + 1, frame.Bottom + FrameMargin + 1);

        protected override void Render(Graphics g)
        {
            k = Math.Min(W / ScreenW, H / ScreenH);
            ox = (W - ScreenW * k) / 2f;
            oy = (H - ScreenH * k) / 2f;

            string key = LayerKey();
            if (layer is null || layer.Size != ClientSize || key != layerKey)
            {
                BuildLayer();
                layerKey = key;
            }

            GraphicsState copy = g.Save();
            g.ResetTransform();
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(layer!, 0, 0, layer!.Width, layer.Height);
            g.Restore(copy);

            DrawAll(g);
        }

        private void BuildLayer()
        {
            layer?.Dispose();
            layer = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using Graphics g = Graphics.FromImage(layer);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(S, S);

            layerPass = true;
            try
            {
                DrawPlate(g);
                DrawAll(g);
            }
            finally
            {
                layerPass = false;
            }
        }

        /// <summary>Everything the cached layer depends on; when any of it changes the layer is repainted.</summary>
        private string LayerKey()
        {
            ConnectionService connection = AppState.Connection;
            ScanModule current = ScanSession.Current;
            return string.Join("|",
                ClientSize, Loc.Language, HoverId, PressedId, cardPage,
                string.Concat(ScanSession.Modules.Select(m => (int)m.State)), current.Key, current.State,
                connection.StatusText, connection.Current.Name, connection.Protocol, connection.IsConnected,
                Vehicle.Vin, Vehicle.Model, Vehicle.Engine, DateTime.Now.ToString("HHmm"),
                AppState.Settings.SpeedUnit, AppState.Settings.TempUnit);
        }

        private void DrawAll(Graphics g)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(k, k);
            g.TranslateTransform(-ScreenX, -ScreenY);

            DrawTopBar(g);
            DrawRail(g);
            DrawScanHeader(g);
            DrawCallouts(g);
            DrawLegend(g);
            DrawAreaPanel(g);
            DrawCards(g);

            g.Restore(state);
        }

        /// <summary>
        /// The plate is scaled once per window size and then copied pixel for pixel, centred; a window of
        /// another shape gets the screen colour in the margins.
        /// </summary>
        private void DrawPlate(Graphics g)
        {
            float pixels = S * k;
            var size = new Size(Math.Max(1, (int)Math.Round(ScreenW * pixels)), Math.Max(1, (int)Math.Round(ScreenH * pixels)));
            if (plate is null || plate.Size != size)
            {
                plate?.Dispose();
                plate = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using Graphics pg = Graphics.FromImage(plate);
                pg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                pg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                pg.CompositingMode = CompositingMode.SourceCopy;
                using var attributes = new System.Drawing.Imaging.ImageAttributes();
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                Bitmap source = ScanKit.Sprite("plate");
                pg.DrawImage(source, new Rectangle(0, 0, size.Width, size.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            }

            GraphicsState state = g.Save();
            g.ResetTransform();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingMode = CompositingMode.SourceCopy;

            int x = (int)Math.Round(ox * S);
            int y = (int)Math.Round(oy * S);
            g.DrawImage(plate, x, y, size.Width, size.Height);

            // Around it, the screen colour at each edge (the rail's highlights must not run into the margin).
            int clientW = ClientSize.Width;
            int clientH = ClientSize.Height;
            using (var leftFill = new SolidBrush(Color.FromArgb(1, 12, 21)))
            using (var rightFill = new SolidBrush(Color.FromArgb(1, 7, 15)))
            {
                g.FillRectangle(leftFill, 0, 0, Math.Max(0, x), clientH);
                g.FillRectangle(rightFill, x + size.Width, 0, Math.Max(0, clientW - x - size.Width), clientH);
                g.FillRectangle(leftFill, x, 0, size.Width, Math.Max(0, y));
                g.FillRectangle(leftFill, x, y + size.Height, size.Width, Math.Max(0, clientH - y - size.Height));
            }

            g.Restore(state);
        }

        // ---- top bar -----------------------------------------------------------------------------

        private void DrawTopBar(Graphics g)
        {
            HitAt(RectangleF.FromLTRB(312, 46, 356, 90), () => Shell.Back(), "scan-back");
            if (!layerPass)
            {
                DrawLanguages(g);
                return;
            }

            if (IsHover("scan-back"))
            {
                Draw.FillRounded(g, Color.FromArgb(22, 255, 255, 255), RectangleF.FromLTRB(316, 50, 352, 86), 9f);
            }

            ScanKit.Text(g, Loc.T("scan.title"), ScanText.HdrTitle, maxWidth: 500);

            // Vehicle line: model, year and engine, a divider, then the VIN - each piece in its own spot.
            string model = string.Join(" ", new[] { Vehicle.Make, Vehicle.Model }.Where(s => s != "-"));
            float x = ScanText.HdrModel.X;
            if (model.Length > 0)
            {
                x = ScanKit.Text(g, model, ScanText.HdrModel, maxWidth: 200) + (ScanText.HdrYear.X - ScanText.HdrModel.Right);
            }

            if (Vehicle.Year != "-")
            {
                x = ScanKit.Text(g, Vehicle.Year, ScanText.HdrYear, x: x) + (ScanText.HdrEngine.X - ScanText.HdrYear.Right);
            }

            if (Vehicle.Engine != "-")
            {
                x = ScanKit.Text(g, Vehicle.Engine, ScanText.HdrEngine, x: x, maxWidth: 160);
            }

            float divider = x + 19f;
            using (var pen = new Pen(Color.FromArgb(70, 88, 112), 1.2f))
            {
                g.DrawLine(pen, divider, 89.5f, divider, 104.5f);
            }

            ScanKit.Text(g, $"VIN: {Vehicle.Vin}", ScanText.HdrVin, x: divider + (ScanText.HdrVin.X - 579f), maxWidth: 880f - divider);

            // Connection pill.
            ConnectionService connection = AppState.Connection;
            Color dot = connection.IsLive ? Color.FromArgb(24, 226, 140)
                : connection.IsDemo ? Color.FromArgb(247, 181, 0)
                : Color.FromArgb(240, 60, 70);
            ScanKit.Dot(g, new PointF(912.5f, 67.5f), 5.8f, dot, true);
            ScanKit.Text(g, connection.StatusText, ScanText.Connected, color: connection.IsLive ? ScanText.Connected.Color : dot, maxWidth: 102);
            ScanKit.Text(g, connection.Current.Name, ScanText.Adapter, maxWidth: 86);

            DrawLanguages(g);

            ScanKit.Text(g, DateTime.Now.ToString("HH:mm"), ScanText.Clock);
        }

        private void DrawLanguages(Graphics g)
        {
            string[] labels = { "EN", "日本語", "中文" };
            int selected = Array.IndexOf(Loc.Languages, Loc.Language);

            for (int i = 0; i < LanguageCells.Length; i++)
            {
                RectangleF cell = LanguageCells[i];
                string id = $"scan-lang-{i}";
                bool active = i == selected;
                int index = i;
                HitAt(cell, () => AppState.Settings.Update(s => s.Language = Loc.Languages[index]), id);
                if (!layerPass)
                {
                    continue;
                }

                if (active)
                {
                    for (int glow = 3; glow >= 1; glow--)
                    {
                        Draw.FillRounded(g, Color.FromArgb(14, 255, 30, 60), RectangleF.Inflate(cell, glow * 1.5f, glow * 1.5f), 7f + glow);
                    }

                    using var fill = new LinearGradientBrush(cell, Color.FromArgb(246, 28, 58), Color.FromArgb(226, 14, 44), LinearGradientMode.Vertical);
                    Draw.FillRounded(g, fill, cell, 6.5f);
                }
                else
                {
                    Draw.FillRounded(g, IsHover(id) ? Color.FromArgb(30, 42, 62) : Color.FromArgb(17, 25, 39), cell, 6.5f);
                    Draw.StrokeRounded(g, Color.FromArgb(34, 46, 64), cell, 6.5f, 1f);
                }

                var spec = ScanText.LangEn with { Center = cell.X + cell.Width / 2f };
                ScanKit.Text(g, labels[i], spec, TextAlign.Center,
                    active ? Color.White : Color.FromArgb(206, 214, 227), style: i == 0 ? null : spec with { Medium = false, Size = 14.4f });
            }
        }

        // ---- navigation rail ----------------------------------------------------------------------

        private void DrawRail(Graphics g)
        {
            for (int i = 0; i < NavRows.Length; i++)
            {
                (string key, string nav, object? argument, float y) = NavRows[i];
                var row = RectangleF.FromLTRB(40, y - 29, 196, y + 29);
                string id = $"scan-nav-{i}";
                HitAt(row, () => Shell.Navigate(nav, argument), id);
                if (!layerPass)
                {
                    continue;
                }

                if (i > 0 && (IsHover(id) || IsPressed(id)))
                {
                    Draw.FillRounded(g, Color.FromArgb(IsPressed(id) ? 34 : 20, 255, 255, 255), row, 8f);
                }

                ScanKit.Text(g, Loc.T(key), NavSpecs[i], maxWidth: 190 - NavSpecs[i].X);
            }
        }

        // ---- scan panel -----------------------------------------------------------------------------

        private void DrawScanHeader(Graphics g)
        {
            var track = RectangleF.FromLTRB(489, 153.5f, 769, 162.5f);
            var refresh = RectangleF.FromLTRB(436, 733, 476, 769);
            HitAt(refresh, () => ScanSession.Start(), "scan-restart");

            if (layerPass)
            {
                ScanKit.Text(g, Loc.T("scan.title"), ScanText.ScanTitle, maxWidth: 195);
                ScanKit.FillCapsule(g, Color.FromArgb(16, 27, 40), track);
                using (var edge = new Pen(Color.FromArgb(60, 78, 100), 1f))
                using (GraphicsPath path = ScanKit.Capsule(track))
                {
                    g.DrawPath(edge, path);
                }

                ScanKit.Text(g, Loc.T("scan.modules", ScanSession.Completed, ScanSession.Modules.Count), ScanText.ScanModules, maxWidth: 160);
                ScanKit.Text(g, Loc.T("scan.elapsed"), ScanText.ElapsedLabel, maxWidth: 110);

                // The refresh button beside 3D / 2D / X-Ray runs the scan again.
                if (IsHover("scan-restart"))
                {
                    Draw.FillRounded(g, Color.FromArgb(22, 255, 255, 255), refresh, 8f);
                }

                return;
            }

            float fraction = ScanSession.Fraction;
            if (fraction > 0f)
            {
                var fill = RectangleF.FromLTRB(track.X, track.Y, track.X + Math.Max(track.Height, track.Width * fraction), track.Bottom);
                ScanKit.Glow(g, fill, Color.FromArgb(255, 20, 45), 7f, 70);
                using var brush = new LinearGradientBrush(fill, Color.FromArgb(250, 16, 38), Color.FromArgb(251, 36, 60), LinearGradientMode.Horizontal);
                ScanKit.FillCapsule(g, brush, fill);
                using var shine = new Pen(Color.FromArgb(110, 255, 150, 150), 1f);
                g.DrawLine(shine, fill.X + 5, fill.Y + 1f, fill.Right - 5, fill.Y + 1f);
            }

            ScanKit.Text(g, $"{Math.Round(fraction * 100)}%", ScanText.ScanPercent);
            ScanKit.Text(g, Clock(ScanSession.Elapsed), ScanText.Elapsed);
        }

        private void DrawCallouts(Graphics g)
        {
            if (!layerPass)
            {
                return;
            }

            foreach ((string key, RectangleF frame, ModuleState drawn, PointF icon, TextSpec title, TextSpec status) in Callouts)
            {
                ScanModule module = ScanSession.Find(key);
                ModuleState state = module.State;

                if (state == drawn)
                {
                    ScanKit.NineSlice(g, $"callout-{key}", Outer(frame), 24f);
                    ScanKit.Draw(g, $"callout-icon-{key}", icon.X, icon.Y);
                }
                else
                {
                    // Another state borrows the frame drawn in it, without the scenery around that frame.
                    ScanKit.NineSlice(g, CalloutFrame(state), RectangleF.Inflate(frame, 2f, 2f), 16f, FrameMargin - 2f);
                    ScanKit.Draw(g, CalloutIcon(state), icon.X, icon.Y);
                }

                ScanKit.Text(g, module.FullName, title, maxWidth: frame.Right - 8 - title.X);
                ScanKit.Text(g, StateText(state), status, color: CalloutStatusColor(state),
                    style: state == drawn ? null : CalloutStatusStyle(state), maxWidth: frame.Right - 8 - status.X);
            }
        }

        private void DrawLegend(Graphics g)
        {
            if (!layerPass)
            {
                return;
            }

            (string Key, TextSpec Spec, float Next)[] items =
            {
                ("scan.legend.scanning", ScanText.LegendScanning, 678),
                ("scan.legend.passed", ScanText.LegendPassed, 760),
                ("scan.legend.warning", ScanText.LegendWarning, 847),
                ("scan.legend.fault", ScanText.LegendFault, 916),
                ("scan.legend.pending", ScanText.LegendPending, 984),
            };

            foreach ((string key, TextSpec spec, float next) in items)
            {
                ScanKit.Text(g, Loc.T(key), spec, maxWidth: next - spec.X);
            }
        }

        // ---- current module -------------------------------------------------------------------------

        private void DrawAreaPanel(Graphics g)
        {
            ScanModule module = ScanSession.Current;
            ModuleState state = module.State;
            var track = RectangleF.FromLTRB(1201, 255, 1433, 263);
            TextSpec[] labels = { ScanText.Live0, ScanText.Live1, ScanText.Live2, ScanText.Live3, ScanText.Live4 };
            TextSpec[] values = { ScanText.LiveVal0, ScanText.LiveVal1, ScanText.LiveVal2, ScanText.LiveVal3, ScanText.LiveVal4 };
            (string Label, string Value, string Pid)[] rows = LiveRows(module);

            if (!layerPass)
            {
                DrawAreaMoving(g, module, track, values, rows);
                return;
            }

            ScanKit.Text(g, Loc.T("scan.area"), ScanText.AreaTitle, maxWidth: 225);

            if (module.Key == "abs" && state is ModuleState.Scanning or ModuleState.Fault)
            {
                ScanKit.Draw(g, "area-tile-abs", 1197, 183);
            }
            else
            {
                ScanKit.Draw(g, $"tile-{module.Tile}-{Palette(state)}", RectangleF.FromLTRB(1199, 185, 1267, 253));
            }

            ScanKit.Text(g, Loc.T("scan.module", module.ShortName), ScanText.AreaModule, maxWidth: 190);
            ScanKit.Text(g, StateText(state), ScanText.AreaStatus, color: AreaStatusColor(state), maxWidth: 190);

            ScanKit.FillCapsule(g, Color.FromArgb(22, 31, 51), track);
            using (var edge = new Pen(Color.FromArgb(46, 62, 88), 1f))
            using (GraphicsPath path = ScanKit.Capsule(track))
            {
                g.DrawPath(edge, path);
            }

            bool live = AppState.Connection.IsLive;
            ScanKit.Text(g, Loc.T("scan.protocol"), ScanText.RowProtocol, maxWidth: 130);
            ScanKit.Text(g, Loc.T("scan.address"), ScanText.RowAddress, maxWidth: 130);
            ScanKit.Text(g, Loc.T("scan.ecuid"), ScanText.RowEcu, maxWidth: 130);
            ScanKit.Text(g, Loc.T("scan.time"), ScanText.RowTime, maxWidth: 130);
            ScanKit.Text(g, ShortProtocol(AppState.Connection.Protocol), ScanText.ValProtocol, maxWidth: 136);
            ScanKit.Text(g, $"0x{module.Address:X2}", ScanText.ValAddress);
            string ecu = live ? (module.Key == "ecm" ? Vehicle.CalibrationId : "-") : AppState.Connection.IsDemo ? module.EcuId : "-";
            ScanKit.Text(g, ecu, ScanText.ValEcu, maxWidth: 136);

            ScanKit.Text(g, Loc.T("scan.live", module.ShortName), ScanText.LiveTitle, maxWidth: 260);
            for (int i = 0; i < rows.Length && i < labels.Length; i++)
            {
                ScanKit.Text(g, rows[i].Label, labels[i], maxWidth: 170);
            }

            bool linked = AppState.Connection.IsConnected;
            ScanKit.Text(g, Loc.T("scan.comm"), ScanText.Communication, maxWidth: 150);
            ScanKit.Text(g, Loc.T(linked ? "scan.stable" : "scan.lost"), ScanText.Stable, TextAlign.Right,
                linked ? ScanText.Stable.Color : Color.FromArgb(240, 70, 80));
        }

        /// <summary>The parts of the right panel that move: progress, timer, live values and the trace.</summary>
        private static void DrawAreaMoving(Graphics g, ScanModule module, RectangleF track, TextSpec[] values, (string Label, string Value, string Pid)[] rows)
        {
            ModuleState state = module.State;
            if (module.Progress > 0f)
            {
                var fill = RectangleF.FromLTRB(track.X, track.Y, track.X + Math.Max(track.Height, track.Width * module.Progress), track.Bottom);
                Color end = state switch
                {
                    ModuleState.Passed => Color.FromArgb(30, 214, 140),
                    ModuleState.Warning => Color.FromArgb(247, 176, 40),
                    ModuleState.Pending => Color.FromArgb(120, 160, 205),
                    _ => Color.FromArgb(250, 48, 72),
                };
                ScanKit.Glow(g, fill, end, 6f, 50);
                using var brush = new LinearGradientBrush(RectangleF.Inflate(fill, 1, 0), Draw.Lerp(end, Color.Black, 0.5f), end, LinearGradientMode.Horizontal);
                ScanKit.FillCapsule(g, brush, fill);
            }

            ScanKit.Text(g, $"{Math.Round(module.Progress * 100)}%", ScanText.AreaPercent, TextAlign.Right);
            ScanKit.Text(g, Clock(module.Elapsed), ScanText.ValTime);
            for (int i = 0; i < rows.Length && i < values.Length; i++)
            {
                ScanKit.Text(g, rows[i].Value, values[i], TextAlign.Right, style: values[1]);
            }

            DrawTrace(g, rows[0].Pid);
        }

        /// <summary>The red trace of the chart: the last minute of the module's first live value.</summary>
        private static void DrawTrace(Graphics g, string pid)
        {
            float[] samples = AppState.Telemetry.HistoryOf(pid).Recent(90);
            if (samples.Length < 2)
            {
                return;
            }

            float min = samples.Min();
            float max = samples.Max();
            float span = Math.Max(max - min, Math.Max(0.5f, Math.Abs(max) * 0.04f));
            float mid = (max + min) / 2f;

            const float left = 1199f;
            const float right = 1466f;
            const float top = 623f;
            const float bottom = 661f;
            var points = new PointF[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (samples[i] - mid) / span;
                points[i] = new PointF(left + (right - left) * i / (samples.Length - 1), (top + bottom) / 2f - t * (bottom - top));
            }

            GraphicsState state = g.Save();
            g.SetClip(RectangleF.FromLTRB(1197, 611, 1469, 675));
            using (var halo = new Pen(Color.FromArgb(46, 255, 30, 50), 5f) { LineJoin = LineJoin.Round })
            {
                g.DrawCurve(halo, points, 0.4f);
            }

            using (var pen = new Pen(Color.FromArgb(238, 22, 48), 1.7f) { LineJoin = LineJoin.Round })
            {
                g.DrawCurve(pen, points, 0.4f);
            }

            g.Restore(state);
        }

        // ---- module cards ------------------------------------------------------------------------------

        private void DrawCards(Graphics g)
        {
            if (layerPass)
            {
                ScanKit.Text(g, Loc.T("scan.modulestitle"), ScanText.ModulesTitle, maxWidth: 300);
            }

            IReadOnlyList<ScanModule> page = CardPage(cardPage);
            for (int i = 0; i < Slots.Length && i < page.Count; i++)
            {
                (string slotKey, float x0, float x1, ModuleState drawn, TextSpec title, TextSpec sub, TextSpec status, TextSpec value) = Slots[i];
                ScanModule module = page[i];
                ModuleState state = module.State;
                bool asDrawn = module.Key == slotKey && state == drawn;
                var card = RectangleF.FromLTRB(x0, CardTop, x1, CardBottom);
                var outer = Outer(card);
                string? system = module.Systems.FirstOrDefault(s => s is "Engine" or "Transmission" or "ABS" or "Airbag");
                string id = $"scan-card-{i}";
                if (system is not null)
                {
                    HitAt(card, () => Shell.Navigate("systemdetail", system), id);
                }

                string valueText = state switch
                {
                    ModuleState.Passed => $"{module.ResponseMs} ms",
                    ModuleState.Scanning => $"{Math.Round(module.Progress * 100)}%",
                    ModuleState.Warning or ModuleState.Fault => Loc.T("scan.codes", module.Codes),
                    _ => "\u2014",
                };
                TextSpec valueStyle = asDrawn ? value : CardValueStyle(state);

                // The scanning card's percentage and bar move; everything else on the cards waits for a state change.
                if (!layerPass)
                {
                    if (state == ModuleState.Scanning)
                    {
                        ScanKit.Text(g, valueText, value, TextAlign.Right, CardValueColor(state), style: valueStyle);
                        DrawCardBar(g, RectangleF.FromLTRB(x0 + 40, CardTop + 92, x1 - 15, CardTop + 98), module.Progress);
                    }

                    continue;
                }

                ScanKit.NineSlice(g, asDrawn ? $"card-{slotKey}" : CardFrame(state), outer, 26f);
                ScanKit.Draw(g, $"tile-{module.Tile}-{Palette(state)}", x0 + 7, CardTop + 6);

                ScanKit.Text(g, module.ShortName, title, maxWidth: x1 - 6 - title.X);
                ScanKit.Text(g, module.CardName, sub, maxWidth: x1 - 4 - sub.X);

                (string icon, PointF offset) = StatusIcon(state);
                ScanKit.Draw(g, icon, x0 + offset.X, CardTop + offset.Y);

                float valueWidth = ScanKit.Measure(g, state == ModuleState.Scanning ? "100%" : valueText, valueStyle);
                ScanKit.Text(g, StateText(state), status, color: CardStatusColor(state),
                    style: asDrawn ? null : CardStatusStyle(state), maxWidth: value.Right - valueWidth - 6 - status.X);
                if (state != ModuleState.Scanning)
                {
                    ScanKit.Text(g, valueText, value, TextAlign.Right, CardValueColor(state), style: valueStyle);
                }

                if (system is not null && IsHover(id))
                {
                    Draw.FillRounded(g, Color.FromArgb(14, 255, 255, 255), card, 10f);
                }
            }

            var next = RectangleF.FromLTRB(1474, 872, 1502, 934);
            if (layerPass && IsHover("scan-cards-next"))
            {
                Draw.FillRounded(g, Color.FromArgb(20, 255, 255, 255), next, 8f);
            }

            HitAt(next, () =>
            {
                cardPage = (cardPage + 1) % PageCount;
                Invalidate();
            }, "scan-cards-next");
        }

        private static void DrawCardBar(Graphics g, RectangleF track, float progress)
        {
            ScanKit.FillCapsule(g, Color.FromArgb(34, 24, 36), track);
            if (progress <= 0f)
            {
                return;
            }

            var fill = RectangleF.FromLTRB(track.X, track.Y, track.X + Math.Max(track.Height, track.Width * progress), track.Bottom);
            ScanKit.Glow(g, fill, Color.FromArgb(255, 30, 60), 5f, 60);
            using var brush = new LinearGradientBrush(RectangleF.Inflate(fill, 1, 0), Color.FromArgb(234, 16, 50), Color.FromArgb(250, 64, 92), LinearGradientMode.Horizontal);
            ScanKit.FillCapsule(g, brush, fill);
        }

        private const int PageCount = 3;

        /// <summary>First page: the seven modules of the mock-up. Then the others, seven at a time.</summary>
        private static IReadOnlyList<ScanModule> CardPage(int page)
        {
            if (page == 0)
            {
                return ScanSession.Featured.Select(ScanSession.Find).ToList();
            }

            return ScanSession.Modules
                .Where(m => !ScanSession.Featured.Contains(m.Key))
                .Skip((page - 1) * 7)
                .Take(7)
                .ToList();
        }

        // ---- state styling ---------------------------------------------------------------------------

        private static string StateText(ModuleState state) => state switch
        {
            ModuleState.Scanning => Loc.T("scan.state.scanning"),
            ModuleState.Passed => Loc.T("scan.state.passed"),
            ModuleState.Warning => Loc.T("scan.state.warning"),
            ModuleState.Fault => Loc.T("scan.state.fault"),
            _ => Loc.T("scan.state.pending"),
        };

        private static string Palette(ModuleState state) => state switch
        {
            ModuleState.Passed => "green",
            ModuleState.Scanning or ModuleState.Fault => "red",
            ModuleState.Warning => "amber",
            _ => "blue",
        };

        private static string CalloutFrame(ModuleState state) => state switch
        {
            ModuleState.Passed => "callout-tcm",
            ModuleState.Scanning or ModuleState.Fault => "callout-abs",
            ModuleState.Warning => "callout-amber",
            _ => "callout-bcm",
        };

        private static string CalloutIcon(ModuleState state) => state switch
        {
            ModuleState.Passed => "callout-icon-tcm",
            ModuleState.Scanning or ModuleState.Fault => "callout-icon-abs",
            ModuleState.Warning => "callout-icon-amber",
            _ => "callout-icon-bcm",
        };

        private static TextSpec CalloutStatusStyle(ModuleState state) => state switch
        {
            ModuleState.Passed => ScanText.CalloutTcmStatus,
            ModuleState.Pending => ScanText.CalloutBcmStatus,
            _ => ScanText.CalloutAbsStatus,
        };

        private static Color CalloutStatusColor(ModuleState state) => state switch
        {
            ModuleState.Passed => ScanText.CalloutTcmStatus.Color,
            ModuleState.Scanning => ScanText.CalloutAbsStatus.Color,
            ModuleState.Fault => Color.FromArgb(255, 112, 124),
            ModuleState.Warning => Color.FromArgb(247, 184, 60),
            _ => ScanText.CalloutBcmStatus.Color,
        };

        private static string CardFrame(ModuleState state) => state switch
        {
            ModuleState.Passed => "card-ecm",
            ModuleState.Scanning or ModuleState.Fault => "card-abs",
            ModuleState.Warning => "card-amber",
            _ => "card-bcm",
        };

        private static TextSpec CardStatusStyle(ModuleState state) => state switch
        {
            ModuleState.Passed => ScanText.CardEcmStatus,
            ModuleState.Pending => ScanText.CardBcmStatus,
            _ => ScanText.CardAbsStatus,
        };

        private static TextSpec CardValueStyle(ModuleState state) => state switch
        {
            ModuleState.Passed => ScanText.CardEcmValue,
            ModuleState.Scanning => ScanText.CardAbsValue,
            ModuleState.Pending => ScanText.CardBcmValue,
            _ => ScanText.CardEcmValue,
        };

        private static Color CardStatusColor(ModuleState state) => state switch
        {
            ModuleState.Passed => ScanText.CardEcmStatus.Color,
            ModuleState.Scanning => ScanText.CardAbsStatus.Color,
            ModuleState.Fault => Color.FromArgb(244, 84, 100),
            ModuleState.Warning => Color.FromArgb(247, 178, 48),
            _ => ScanText.CardSrsStatus.Color,
        };

        private static Color CardValueColor(ModuleState state) => state switch
        {
            ModuleState.Passed => ScanText.CardEcmValue.Color,
            ModuleState.Scanning => ScanText.CardAbsValue.Color,
            ModuleState.Fault => Color.FromArgb(244, 84, 100),
            ModuleState.Warning => Color.FromArgb(247, 178, 48),
            _ => ScanText.CardSrsValue.Color,
        };

        private static Color AreaStatusColor(ModuleState state) => state switch
        {
            ModuleState.Passed => Color.FromArgb(60, 214, 150),
            ModuleState.Warning => Color.FromArgb(247, 184, 60),
            ModuleState.Fault => Color.FromArgb(255, 112, 124),
            ModuleState.Pending => Color.FromArgb(150, 182, 214),
            _ => ScanText.AreaStatus.Color,
        };

        private static (string Sprite, PointF Offset) StatusIcon(ModuleState state) => state switch
        {
            ModuleState.Passed => ("status-check", ScanText.StatusCheckOffset),
            ModuleState.Scanning or ModuleState.Fault => ("status-alert", ScanText.StatusAlertOffset),
            ModuleState.Warning => ("status-amber", ScanText.StatusAlertOffset),
            _ => ("status-pending", ScanText.StatusPendingOffset),
        };

        // ---- values ---------------------------------------------------------------------------------------

        /// <summary>
        /// Five live rows for the module on the right. OBD2 has no per-wheel speeds, so the four wheel rows
        /// are simulated around the vehicle speed in demo mode and replaced by engine values on a real car.
        /// </summary>
        private static (string Label, string Value, string Pid)[] LiveRows(ScanModule module)
        {
            Telemetry t = AppState.Telemetry;
            AppSettings settings = AppState.Settings;
            string speed(float kmh) => $"{settings.Speed(kmh):0.0} {settings.SpeedUnit}";
            var voltage = (Loc.T("scan.row.voltage"), $"{t.BatteryVoltage:0.0} V", "battery");
            var vehicleSpeed = (Loc.T("scan.row.speed"), speed(t.Speed), "speed");
            var rpm = (Loc.T("scan.row.rpm"), $"{t.Rpm:0} rpm", "rpm");
            var load = (Loc.T("scan.row.load"), $"{t.EngineLoad:0} %", "load");
            var throttle = (Loc.T("scan.row.throttle"), $"{t.Throttle:0} %", "throttle");
            var coolant = (Loc.T("scan.row.coolant"), $"{settings.Temperature(t.CoolantTemp):0} {settings.TempUnit}", "coolant");
            var intake = (Loc.T("scan.row.intake"), $"{settings.Temperature(t.IntakeTemp):0} {settings.TempUnit}", "intake");
            var ambient = (Loc.T("scan.row.ambient"), $"{settings.Temperature(t.AmbientTemp):0} {settings.TempUnit}", "ambient");

            switch (module.Key)
            {
                case "abs" when AppState.Connection.IsDemo:
                    // Demo wheels differ by a percent or two, the way real wheel speed sensors do.
                    float wobble = (float)Math.Sin(Environment.TickCount64 / 900.0) * 0.004f;
                    return new[]
                    {
                        (Loc.T("scan.row.fl"), speed(t.Speed * (0.992f + wobble)), "speed"),
                        (Loc.T("scan.row.fr"), speed(t.Speed * (1.016f - wobble)), "speed"),
                        (Loc.T("scan.row.rl"), speed(t.Speed * (0.968f + wobble)), "speed"),
                        (Loc.T("scan.row.rr"), speed(t.Speed * (0.984f - wobble)), "speed"),
                        voltage,
                    };
                case "abs":
                case "tcm":
                case "eps":
                    return new[] { vehicleSpeed, rpm, throttle, load, voltage };
                case "ecm":
                case "hvb":
                case "mg":
                    return new[] { rpm, load, coolant, throttle, voltage };
                case "hvac":
                case "aud":
                case "seat":
                    return new[] { ambient, intake, coolant, vehicleSpeed, voltage };
                default:
                    return new[] { vehicleSpeed, rpm, coolant, ambient, voltage };
            }
        }

        private static string ShortProtocol(string protocol)
        {
            if (string.IsNullOrWhiteSpace(protocol) || protocol == "-")
            {
                return "-";
            }

            string p = protocol.ToUpperInvariant();
            if (p.Contains("CAN") || p.Contains("15765"))
            {
                return p.Contains("250") ? "CAN 250 kbps" : "CAN 500 kbps";
            }

            if (p.Contains("PWM"))
            {
                return "J1850 PWM";
            }

            if (p.Contains("VPW"))
            {
                return "J1850 VPW";
            }

            if (p.Contains("9141"))
            {
                return "ISO 9141-2";
            }

            return p.Contains("14230") || p.Contains("KWP") ? "KWP2000" : protocol;
        }

        private static string Clock(TimeSpan span) =>
            span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ScanSession.Changed -= OnScanChanged;
                plate?.Dispose();
                layer?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

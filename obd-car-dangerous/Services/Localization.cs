using System.Drawing.Text;

namespace obd_car_dangerous.Services
{
    /// <summary>
    /// UI translations for English, Japanese and Chinese. Every user visible string goes through
    /// <see cref="T(string)"/>; switching language also switches the font family so CJK text renders.
    /// </summary>
    internal static class Loc
    {
        /// <summary>Language names, shown as-is in Settings.</summary>
        public static readonly string[] Languages = { "English", "日本語", "中文" };

        private static int index;

        public static event EventHandler? Changed;

        public static string Language => Languages[index];

        public static bool IsCjk => index > 0;

        /// <summary>Font family that covers the current language.</summary>
        public static string FontFamily { get; private set; } = Pick("Segoe UI", "Tahoma", "Arial");

        public static void Set(string language)
        {
            int found = Array.IndexOf(Languages, language);
            if (found < 0 || found == index)
            {
                return;
            }

            index = found;
            FontFamily = index switch
            {
                1 => Pick("Yu Gothic UI", "Meiryo UI", "MS UI Gothic", "Segoe UI"),
                2 => Pick("Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Segoe UI"),
                _ => Pick("Segoe UI", "Tahoma", "Arial"),
            };

            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Translated text for a key. Unknown keys fall back to the key itself.</summary>
        public static string T(string key) =>
            Table.TryGetValue(key, out string[]? values) ? values[index] : key;

        public static string T(string key, params object?[] args) => string.Format(T(key), args);

        /// <summary>Label of a live parameter, translated when a translation exists.</summary>
        public static string Pid(string pidKey, string fallback) =>
            Table.TryGetValue("pid." + pidKey, out string[]? values) ? values[index] : fallback;

        /// <summary>Severity word (High / Medium / Low) in the current language.</summary>
        public static string Severity(string severity) => severity switch
        {
            "High" => T("severity.high"),
            "Medium" => T("severity.medium"),
            _ => T("severity.low"),
        };

        /// <summary>Good / Warning / Fault in the current language.</summary>
        public static string StatusWord(string status) => status switch
        {
            "Good" => T("common.good"),
            "Warning" => T("common.warning"),
            _ => T("common.fault"),
        };

        public static string Status(DtcStatus status) => status switch
        {
            DtcStatus.Current => T("dtc.current"),
            DtcStatus.Pending => T("dtc.pending"),
            _ => T("dtc.history"),
        };

        public static string SystemName(string system) => system switch
        {
            "Engine" => T("sys.engine"),
            "Transmission" => T("sys.transmission"),
            "ABS" => T("sys.abs"),
            "Airbag" => T("sys.airbag"),
            "Battery" => T("sys.battery"),
            "Emission" => T("sys.emission"),
            "Network" => T("sys.network"),
            "Body" => T("sys.body"),
            "Chassis" => T("sys.chassis"),
            _ => system,
        };

        private static string Pick(params string[] candidates)
        {
            using var installed = new InstalledFontCollection();
            string[] names = installed.Families.Select(f => f.Name).ToArray();
            foreach (string candidate in candidates)
            {
                if (names.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return candidates[^1];
        }

        // en, ja, zh
        private static readonly Dictionary<string, string[]> Table = new()
        {
            ["app.title"] = new[] { "OBD2 Car Dangerous System", "OBD2 車両危険検知システム", "OBD2 车辆危险检测系统" },
            ["app.tagline"] = new[] { "Monitor  ·  Detect  ·  Protect", "監視  ·  検知  ·  保護", "监控  ·  检测  ·  保护" },

            // Splash
            ["splash.starting"] = new[] { "Starting...", "起動中...", "正在启动..." },
            ["splash.connecting"] = new[] { "Connecting to OBD2 adapter...", "OBD2 アダプタに接続中...", "正在连接 OBD2 诊断器..." },
            ["splash.reading"] = new[] { "Reading ECU information...", "ECU 情報を読み取り中...", "正在读取 ECU 信息..." },
            ["splash.ready"] = new[] { "Ready", "準備完了", "准备就绪" },

            // Navigation
            ["nav.menu"] = new[] { "MENU", "メニュー", "菜单" },
            ["nav.home"] = new[] { "Home", "ホーム", "主页" },
            ["nav.diagnostics"] = new[] { "Diagnostics", "診断", "诊断" },
            ["nav.livedata"] = new[] { "Live Data", "ライブデータ", "实时数据" },
            ["nav.dtc"] = new[] { "DTC Codes", "故障コード", "故障码" },
            ["nav.dictionary"] = new[] { "Dictionary", "コード辞典", "故障码词典" },
            ["nav.fuel"] = new[] { "Fuel", "燃費", "油耗" },
            ["nav.trip"] = new[] { "Trip Info", "トリップ情報", "行程信息" },
            ["nav.alarms"] = new[] { "Alarm History", "警報履歴", "报警历史" },
            ["nav.settings"] = new[] { "Settings", "設定", "设置" },

            // Shared words
            ["state.connected"] = new[] { "Connected", "接続済み", "已连接" },
            ["state.connecting"] = new[] { "Connecting", "接続中", "连接中" },
            ["state.disconnected"] = new[] { "Disconnected", "未接続", "未连接" },
            ["common.good"] = new[] { "Good", "良好", "良好" },
            ["common.excellent"] = new[] { "Excellent", "優秀", "优秀" },
            ["common.fair"] = new[] { "Fair", "普通", "一般" },
            ["common.poor"] = new[] { "Poor", "不良", "较差" },
            ["common.warning"] = new[] { "Warning", "警告", "警告" },
            ["common.fault"] = new[] { "Fault", "故障", "故障" },
            ["common.critical"] = new[] { "Critical", "重大", "严重" },
            ["common.info"] = new[] { "Info", "情報", "信息" },
            ["common.all"] = new[] { "All", "すべて", "全部" },
            ["common.cancel"] = new[] { "Cancel", "キャンセル", "取消" },
            ["common.health"] = new[] { "HEALTH", "健全度", "健康度" },
            ["common.healthline"] = new[] { "Health {0}/100 · {1}", "健全度 {0}/100 · {1}", "健康度 {0}/100 · {1}" },
            ["common.justnow"] = new[] { "just now", "たった今", "刚刚" },
            ["common.minago"] = new[] { "{0} min ago", "{0} 分前", "{0} 分钟前" },
            ["common.hourago"] = new[] { "{0} h ago", "{0} 時間前", "{0} 小时前" },
            ["common.dayago"] = new[] { "{0} d ago", "{0} 日前", "{0} 天前" },

            ["severity.high"] = new[] { "High", "高", "高" },
            ["severity.medium"] = new[] { "Medium", "中", "中" },
            ["severity.low"] = new[] { "Low", "低", "低" },

            ["sys.engine"] = new[] { "Engine", "エンジン", "发动机" },
            ["sys.transmission"] = new[] { "Transmission", "トランスミッション", "变速箱" },
            ["sys.abs"] = new[] { "ABS", "ABS", "ABS" },
            ["sys.airbag"] = new[] { "Airbag", "エアバッグ", "安全气囊" },
            ["sys.battery"] = new[] { "Battery", "バッテリー", "蓄电池" },
            ["sys.emission"] = new[] { "Emission", "排出ガス", "排放" },
            ["sys.network"] = new[] { "Network", "ネットワーク", "网络" },
            ["sys.body"] = new[] { "Body", "ボディ", "车身" },
            ["sys.chassis"] = new[] { "Chassis", "シャシ", "底盘" },

            // Home
            ["home.status"] = new[] { "Vehicle Status", "車両ステータス", "车辆状态" },
            ["home.normal"] = new[] { "Normal", "正常", "正常" },
            ["home.attention"] = new[] { "Attention", "注意", "注意" },
            ["home.danger"] = new[] { "Danger", "危険", "危险" },
            ["home.nofault"] = new[] { "No critical fault detected!", "重大な故障はありません", "未检测到严重故障！" },
            ["home.faults"] = new[] { "{0} active fault code(s) - tap to review", "現在 {0} 件の故障コード - タップして確認", "当前 {0} 个故障码 - 点击查看" },
            ["home.lastscan"] = new[] { "Last scan {0}", "最終スキャン {0}", "上次扫描 {0}" },
            ["home.trip"] = new[] { "Trip", "トリップ", "行程" },

            // Live data
            ["live.title"] = new[] { "Live Data", "ライブデータ", "实时数据" },
            ["live.streaming"] = new[] { "streaming", "受信中", "接收中" },
            ["live.offline"] = new[] { "adapter offline", "アダプタ未接続", "诊断器离线" },
            ["live.fullgraph"] = new[] { "Full graph", "詳細グラフ", "完整图表" },
            ["live.waiting"] = new[] { "Waiting for data...", "データ待機中...", "等待数据..." },
            ["group.Engine"] = new[] { "Engine", "エンジン", "发动机" },
            ["group.Sensors"] = new[] { "Sensors", "センサー", "传感器" },
            ["group.Fuel"] = new[] { "Fuel", "燃料", "燃油" },
            ["group.Emission"] = new[] { "Emission", "排出ガス", "排放" },
            ["group.Other"] = new[] { "Other", "その他", "其他" },

            ["pid.rpm"] = new[] { "Engine RPM", "エンジン回転数", "发动机转速" },
            ["pid.speed"] = new[] { "Vehicle Speed", "車速", "车速" },
            ["pid.coolant"] = new[] { "Coolant Temp", "冷却水温", "冷却液温度" },
            ["pid.intake"] = new[] { "Intake Air Temp", "吸気温度", "进气温度" },
            ["pid.load"] = new[] { "Engine Load", "エンジン負荷", "发动机负荷" },
            ["pid.throttle"] = new[] { "Throttle Position", "スロットル開度", "节气门开度" },
            ["pid.maf"] = new[] { "MAF Air Flow", "吸入空気量", "空气流量" },
            ["pid.map"] = new[] { "Intake Manifold", "吸気管圧", "进气歧管压力" },
            ["pid.o2"] = new[] { "O2 Sensor B1S1", "O2 センサー B1S1", "氧传感器 B1S1" },
            ["pid.timing"] = new[] { "Timing Advance", "点火進角", "点火提前角" },
            ["pid.oil"] = new[] { "Oil Temp", "油温", "机油温度" },
            ["pid.battery"] = new[] { "Battery Voltage", "バッテリー電圧", "蓄电池电压" },
            ["pid.fuellevel"] = new[] { "Fuel Level", "燃料残量", "燃油液位" },
            ["pid.fuelrate"] = new[] { "Fuel Rate", "燃料消費率", "燃油消耗率" },
            ["pid.consumption"] = new[] { "Instant Economy", "瞬時燃費", "瞬时油耗" },
            ["pid.fuelpressure"] = new[] { "Fuel Pressure", "燃料圧", "燃油压力" },
            ["pid.stft"] = new[] { "Short Fuel Trim", "短期燃料補正", "短期燃油修正" },
            ["pid.ltft"] = new[] { "Long Fuel Trim", "長期燃料補正", "长期燃油修正" },
            ["pid.catalyst"] = new[] { "Catalyst Temp", "触媒温度", "催化器温度" },
            ["pid.egr"] = new[] { "EGR Error", "EGR 誤差", "EGR 误差" },
            ["pid.evap"] = new[] { "EVAP Pressure", "EVAP 圧力", "EVAP 压力" },
            ["pid.o2trim"] = new[] { "O2 Trim B1", "O2 補正 B1", "氧传感器修正 B1" },
            ["pid.ambient"] = new[] { "Ambient Temp", "外気温", "环境温度" },
            ["pid.baro"] = new[] { "Barometric", "大気圧", "大气压" },
            ["pid.distance"] = new[] { "Trip Distance", "走行距離", "行驶里程" },
            ["pid.runtime"] = new[] { "Engine Runtime", "エンジン稼働時間", "发动机运行时间" },

            // Graph
            ["graph.title"] = new[] { "Live Data Graph", "ライブデータグラフ", "实时数据图表" },
            ["graph.now"] = new[] { "Now", "現在", "当前" },
            ["graph.min"] = new[] { "Min", "最小", "最小" },
            ["graph.max"] = new[] { "Max", "最大", "最大" },
            ["graph.avg"] = new[] { "Avg", "平均", "平均" },
            ["graph.range"] = new[] { "{0} min", "{0} 分", "{0} 分钟" },

            // Diagnostics
            ["diag.title"] = new[] { "Vehicle Health", "車両の健全度", "车辆健康" },
            ["diag.overall"] = new[] { "Overall Health", "総合評価", "整体健康" },
            ["diag.lastcheck"] = new[] { "Last Check", "最終チェック", "上次检查" },
            ["diag.fullscan"] = new[] { "Full Scan", "フルスキャン", "全面扫描" },
            ["diag.scanning"] = new[] { "Scanning {0}...", "{0} をスキャン中...", "正在扫描 {0}..." },
            ["diag.reading"] = new[] { "Reading module...", "モジュール読み取り中...", "正在读取模块..." },
            ["diag.nofault"] = new[] { "No fault detected", "故障はありません", "未检测到故障" },
            ["diag.activecodes"] = new[] { "{0} active code(s): {1}", "現在の故障 {0} 件: {1}", "{0} 个当前故障码: {1}" },
            ["diag.batterydetail"] = new[] { "Resting voltage {0} V, charging {1}", "電圧 {0} V、充電 {1}", "电压 {0} V，充电 {1}" },
            ["diag.charging.ok"] = new[] { "OK", "正常", "正常" },
            ["diag.charging.idle"] = new[] { "idle", "アイドル", "怡速" },
            ["diag.offline.title"] = new[] { "Adapter offline", "アダプタ未接続", "诊断器离线" },
            ["diag.offline.body"] = new[]
            {
                "Connect to the OBD2 adapter before running a full system scan.",
                "フルスキャンを実行する前に OBD2 アダプタに接続してください。",
                "进行全面扫描前请先连接 OBD2 诊断器。",
            },
            ["diag.offline.ok"] = new[] { "Open connection", "接続設定へ", "打开连接设置" },

            // System detail
            ["sysdetail.title"] = new[] { "{0} System", "{0} システム", "{0} 系统" },
            ["sysdetail.codes"] = new[] { "Fault codes", "故障コード", "故障码" },
            ["sysdetail.readings"] = new[] { "Live readings", "ライブ値", "实时读数" },
            ["sysdetail.nocodes"] = new[] { "No codes stored for this module.", "このモジュールにコードはありません。", "该模块没有存储故障码。" },
            ["sysdetail.score"] = new[] { "Module score {0}/100", "モジュール評価 {0}/100", "模块评分 {0}/100" },

            // DTC list
            ["dtc.title"] = new[] { "DTC Codes", "故障コード", "故障码" },
            ["dtc.current"] = new[] { "Current", "現在", "当前" },
            ["dtc.pending"] = new[] { "Pending", "保留", "待定" },
            ["dtc.history"] = new[] { "History", "履歴", "历史" },
            ["dtc.tab"] = new[] { "{0} ({1})", "{0} ({1})", "{0} ({1})" },
            ["dtc.clearall"] = new[] { "Clear DTC Codes", "故障コードを消去", "清除故障码" },
            ["dtc.nothing"] = new[] { "Nothing to clear", "消去するコードはありません", "没有可清除的故障码" },
            ["dtc.empty.current"] = new[]
            {
                "No current fault codes - the vehicle reports no active problems.",
                "現在の故障コードはありません。車両は正常です。",
                "没有当前故障码，车辆状态正常。",
            },
            ["dtc.empty.pending"] = new[]
            {
                "No pending codes. Intermittent faults appear here before they become current.",
                "保留コードはありません。間欠的な故障はここに表示されます。",
                "没有待定故障码。间歇性故障会先显示在这里。",
            },
            ["dtc.empty.history"] = new[] { "History is empty.", "履歴はありません。", "历史记录为空。" },
            ["dtc.clear.title"] = new[] { "Clear fault codes?", "故障コードを消去しますか？", "确认清除故障码？" },
            ["dtc.clear.body"] = new[]
            {
                "This sends Mode 04 to the ECU. Current and pending codes move to history and the check engine light resets. Codes come back if the fault is still present.",
                "ECU にモード 04 を送信します。現在・保留コードは履歴に移り、警告灯がリセットされます。故障が残っていれば再発生します。",
                "将向 ECU 发送模式 04。当前和待定故障码将转入历史，故障灯复位。若故障仍存在，代码会再次出现。",
            },
            ["dtc.clear.ok"] = new[] { "Clear codes", "消去する", "清除" },

            // DTC detail
            ["detail.title"] = new[] { "DTC Details", "故障コード詳細", "故障码详情" },
            ["detail.none"] = new[] { "No fault code selected.", "コードが選択されていません。", "未选择故障码。" },
            ["detail.description"] = new[] { "Description", "説明", "说明" },
            ["detail.status"] = new[] { "Status", "状態", "状态" },
            ["detail.severity"] = new[] { "Severity", "重要度", "严重程度" },
            ["detail.system"] = new[] { "System", "システム", "系统" },
            ["detail.detected"] = new[] { "Detected", "検出時刻", "检测时间" },
            ["detail.effect"] = new[] { "Effect", "影響", "影响" },
            ["detail.causes"] = new[] { "Possible Causes", "考えられる原因", "可能原因" },
            ["detail.cleared"] = new[] { "Cleared / stored", "消去済み / 保存", "已清除 / 已存储" },
            ["detail.freeze"] = new[] { "Freeze Frame", "フリーズフレーム", "冻结帧" },
            ["detail.freezehint"] = new[]
            {
                "Sensor snapshot stored when the code set",
                "コード発生時のセンサー値",
                "故障码产生时的传感器快照",
            },
            ["detail.nofreeze"] = new[]
            {
                "No freeze frame stored for this code.",
                "このコードのフリーズフレームはありません。",
                "该故障码没有冻结帧数据。",
            },
            ["detail.opengraph"] = new[] { "Open live graph", "ライブグラフを開く", "打开实时图表" },
            ["detail.clear"] = new[] { "Clear DTC", "このコードを消去", "清除故障码" },
            ["detail.clear.title"] = new[] { "Clear {0}?", "{0} を消去しますか？", "确认清除 {0}？" },
            ["detail.clear.body"] = new[]
            {
                "The code moves to history. If the underlying fault is still present the ECU will set it again on the next drive cycle.",
                "コードは履歴に移ります。故障が続いている場合、次の走行で再度登録されます。",
                "代码将转入历史。若故障仍存在，ECU 会在下次行驶循环中再次记录。",
            },
            ["detail.clear.ok"] = new[] { "Clear code", "消去する", "清除" },

            // Danger overlay
            ["danger.title"] = new[] { "Danger Alert!", "危険警告！", "危险警报！" },
            ["danger.details"] = new[] { "View Details", "詳細を見る", "查看详情" },
            ["danger.clear"] = new[] { "Clear", "消去", "清除" },
            ["danger.dismiss"] = new[] { "Dismiss", "閉じる", "关闭" },
            ["danger.esc"] = new[] { "Esc closes this alert", "Esc で閉じる", "按 Esc 关闭" },
            ["danger.meta"] = new[] { "Severity {0}  ·  {1}  ·  {2}", "重要度 {0}  ·  {1}  ·  {2}", "严重程度 {0}  ·  {1}  ·  {2}" },

            // Fuel
            ["fuel.title"] = new[] { "Fuel Consumption", "燃費", "燃油消耗" },
            ["fuel.average"] = new[] { "Average", "平均", "平均" },
            ["fuel.instant"] = new[] { "Instant", "瞬時", "瞬时" },
            ["fuel.trip"] = new[] { "Trip", "トリップ", "行程" },
            ["fuel.avgeconomy"] = new[] { "Average Economy", "平均燃費", "平均油耗" },
            ["fuel.insteconomy"] = new[] { "Instant Economy", "瞬時燃費", "瞬时油耗" },
            ["fuel.usedtrip"] = new[] { "Fuel Used (trip)", "使用燃料 (トリップ)", "行程用油量" },
            ["fuel.level"] = new[] { "Fuel Level", "燃料残量", "燃油液位" },
            ["fuel.range"] = new[] { "Range approx. {0} {1}", "航続距離 約 {0} {1}", "续航约 {0} {1}" },
            ["fuel.chart"] = new[] { "Fuel Economy ({0})", "燃費 ({0})", "燃油经济性 ({0})" },
            ["fuel.buckets"] = new[] { "10 minute buckets", "10 分ごと", "每 10 分钟" },
            ["fuel.instchart"] = new[] { "Instant economy ({0})", "瞬時燃費 ({0})", "瞬时油耗 ({0})" },
            ["fuel.thistrip"] = new[] { "This trip", "今回のトリップ", "本次行程" },
            ["fuel.distance"] = new[] { "Distance", "走行距離", "行驶距离" },
            ["fuel.drivingtime"] = new[] { "Driving time", "走行時間", "行驶时间" },
            ["fuel.used"] = new[] { "Fuel used", "使用燃料", "用油量" },
            ["fuel.avgspeed"] = new[] { "Average speed", "平均速度", "平均速度" },
            ["fuel.idlerate"] = new[] { "Idle fuel rate", "アイドル燃料量", "怡速油耗" },
            ["fuel.co2"] = new[] { "CO₂ estimate", "CO₂ 推定量", "CO₂ 估算" },
            ["fuel.cost"] = new[] { "Cost at 1.75/L", "燃料代 (1.75/L)", "油费 (1.75/L)" },

            // Trip
            ["trip.title"] = new[] { "Trip Information", "トリップ情報", "行程信息" },
            ["trip.distance"] = new[] { "Distance", "走行距離", "行驶距离" },
            ["trip.time"] = new[] { "Driving Time", "走行時間", "行驶时间" },
            ["trip.avgspeed"] = new[] { "Avg Speed", "平均速度", "平均速度" },
            ["trip.maxspeed"] = new[] { "Max Speed", "最高速度", "最高速度" },
            ["trip.profile"] = new[] { "Speed profile ({0})", "速度推移 ({0})", "速度曲线 ({0})" },
            ["trip.started"] = new[] { "Trip started", "開始時刻", "行程开始" },
            ["trip.fuelused"] = new[] { "Fuel used", "使用燃料", "用油量" },
            ["trip.reset"] = new[] { "Reset trip", "トリップをリセット", "重置行程" },
            ["trip.reset.title"] = new[] { "Reset trip data?", "トリップをリセットしますか？", "确认重置行程数据？" },
            ["trip.reset.body"] = new[]
            {
                "Distance, driving time and maximum speed return to zero. Fault codes and settings are not affected.",
                "走行距離・時間・最高速度が 0 に戻ります。故障コードと設定は変わりません。",
                "行驶距离、时间和最高速度将归零。故障码和设置不受影响。",
            },
            ["trip.reset.ok"] = new[] { "Reset", "リセット", "重置" },

            // Alarm history
            ["alarms.title"] = new[] { "Alarm History", "警報履歴", "报警历史" },
            ["alarms.empty"] = new[] { "No alarms recorded for this filter.", "該当する警報はありません。", "该筛选下没有报警记录。" },
            ["alarms.cleared"] = new[] { "{0} fault code(s) cleared by user", "ユーザーが {0} 件のコードを消去", "用户清除了 {0} 个故障码" },

            // Dictionary
            ["dict.title"] = new[] { "OBD2 Dictionary", "OBD2 コード辞典", "OBD2 故障码词典" },
            ["dict.search"] = new[] { "Search code or description", "コードまたは説明で検索", "搜索代码或说明" },
            ["dict.hint"] = new[]
            {
                "Type to search · click a code for detail",
                "入力して検索 · コードを選ぶと詳細表示",
                "输入即可搜索 · 点击代码查看详情",
            },
            ["dict.count"] = new[] { "{0} of {1} codes", "{1} 件中 {0} 件", "{1} 中的 {0} 条" },
            ["dict.nomatch"] = new[] { "No code matches that search.", "該当するコードはありません。", "没有匹配的故障码。" },
            ["dict.powertrain"] = new[] { "Powertrain (P)", "パワートレイン (P)", "动力系统 (P)" },
            ["dict.body"] = new[] { "Body (B)", "ボディ (B)", "车身 (B)" },
            ["dict.chassis"] = new[] { "Chassis (C)", "シャシ (C)", "底盘 (C)" },
            ["dict.network"] = new[] { "Network (U)", "ネットワーク (U)", "网络 (U)" },
            ["dict.invehicle"] = new[] { "Stored in this vehicle", "この車両に存在", "本车已存储" },
            ["dict.opendetail"] = new[] { "Open vehicle record", "車両の記録を開く", "打开车辆记录" },
            ["dict.english"] = new[]
            {
                "Code descriptions follow the SAE J2012 English wording used by scan tools.",
                "コード説明は SAE J2012 の英語表記です。",
                "代码说明采用 SAE J2012 英文表述。",
            },

            // Settings
            ["set.title"] = new[] { "Settings", "設定", "设置" },
            ["set.general"] = new[] { "General", "一般", "通用" },
            ["set.connection"] = new[] { "OBD2 Connection", "OBD2 接続", "OBD2 连接" },
            ["set.alerts"] = new[] { "Alerts & Notifications", "警報と通知", "报警与通知" },
            ["set.units"] = new[] { "Units", "単位", "单位" },
            ["set.vehicle"] = new[] { "Vehicle Info", "車両情報", "车辆信息" },
            ["set.about"] = new[] { "About", "アプリ情報", "关于" },
            ["set.general.title"] = new[] { "General Settings", "一般設定", "通用设置" },
            ["set.autoconnect"] = new[] { "Auto Connect", "自動接続", "自动连接" },
            ["set.autoconnect.hint"] = new[]
            {
                "Link to the last adapter when the app starts",
                "起動時に前回のアダプタへ接続",
                "启动时连接上次的诊断器",
            },
            ["set.alertsound"] = new[] { "Alert Sound", "警報音", "报警声音" },
            ["set.alertsound.hint"] = new[]
            {
                "Play a sound when a danger alert appears",
                "危険警告時に音を鳴らす",
                "出现危险警报时播放提示音",
            },
            ["set.darkmode"] = new[] { "Dark Mode", "ダークモード", "深色模式" },
            ["set.darkmode.hint"] = new[] { "Easier on the eyes at night", "夜間の視認性を改善", "夜间更护眼" },
            ["set.keepscreen"] = new[] { "Keep Screen On", "画面を常にオン", "保持屏幕常亮" },
            ["set.keepscreen.hint"] = new[]
            {
                "Stop Windows blanking the display while driving",
                "走行中に画面が消えないようにする",
                "行驶时防止屏幕自动关闭",
            },
            ["set.language"] = new[] { "Language", "言語", "语言" },
            ["set.timeout"] = new[] { "Screen Timeout", "画面タイムアウト", "屏幕超时" },
            ["set.timeout.never"] = new[] { "Never", "なし", "永不" },
            ["set.timeout.minutes"] = new[] { "{0} min", "{0} 分", "{0} 分钟" },
            ["set.fullscreen.enter"] = new[] { "Enter full screen", "全画面にする", "进入全屏" },
            ["set.fullscreen.leave"] = new[] { "Leave full screen", "全画面を解除", "退出全屏" },
            ["set.shortcuts"] = new[]
            {
                "F11 full screen · Esc leaves it · Ctrl+D theme",
                "F11 全画面 · Esc 解除 · Ctrl+D テーマ",
                "F11 全屏 · Esc 退出 · Ctrl+D 主题",
            },

            ["conn.protocol"] = new[] { "Protocol: {0}", "プロトコル: {0}", "协议: {0}" },
            ["conn.nolink"] = new[] { "No adapter link", "アダプタ未接続", "未连接诊断器" },
            ["conn.connect"] = new[] { "Connect", "接続", "连接" },
            ["conn.disconnect"] = new[] { "Disconnect", "切断", "断开" },
            ["conn.scan"] = new[] { "Scan", "スキャン", "扫描" },
            ["conn.scanning"] = new[] { "Scanning...", "スキャン中...", "扫描中..." },
            ["conn.available"] = new[] { "Available adapters", "利用可能なアダプタ", "可用诊断器" },
            ["conn.tap"] = new[] { "Tap to connect", "タップで接続", "点击连接" },
            ["conn.signal"] = new[] { "Signal {0}%  ·  {1}", "電波 {0}%  ·  {1}", "信号 {0}%  ·  {1}" },

            ["alerts.title"] = new[] { "Alerts & Notifications", "警報と通知", "报警与通知" },
            ["alert.popup"] = new[] { "Danger Alert Screen", "危険警告画面", "危险警报界面" },
            ["alert.popup.hint"] = new[]
            {
                "Show the full screen warning when a serious fault appears",
                "重大な故障時に全画面で警告",
                "出现严重故障时全屏提醒",
            },
            ["alert.newdtc"] = new[] { "New Fault Codes", "新しい故障コード", "新故障码" },
            ["alert.newdtc.hint"] = new[]
            {
                "Watch the ECU for codes while driving",
                "走行中に ECU を監視",
                "行驶中持续监控 ECU",
            },
            ["alert.overheat"] = new[] { "Overheat Warning", "水温警告", "高温警告" },
            ["alert.overheat.hint"] = new[]
            {
                "Warn when the coolant passes the limit below",
                "冷却水温が下の上限を超えたら警告",
                "冷却液温度超过下方限值时报警",
            },
            ["alert.overspeed"] = new[] { "Over Speed Warning", "速度超過警告", "超速警告" },
            ["alert.overspeed.hint"] = new[]
            {
                "Warn when the vehicle passes the speed limit below",
                "車速が下の上限を超えたら警告",
                "车速超过下方限值时报警",
            },
            ["alert.speedlimit"] = new[] { "Speed limit", "速度上限", "速度限值" },
            ["alert.coolantlimit"] = new[] { "Coolant limit", "水温上限", "水温限值" },
            ["alert.rpmlimit"] = new[] { "RPM limit", "回転数上限", "转速限值" },
            ["alert.test"] = new[] { "Test danger alert", "警告画面をテスト", "测试危险警报" },
            ["alert.test.body"] = new[]
            {
                "This is a test of the danger alert screen. Real alerts show the fault code and what it means.",
                "これは警告画面のテストです。実際の警告では故障コードと内容を表示します。",
                "这是危险警报界面的测试。实际警报会显示故障码及其含义。",
            },

            ["units.system"] = new[] { "Measurement system", "単位系", "单位制" },
            ["units.metric"] = new[] { "Metric (km, L)", "メートル法 (km, L)", "公制 (km, L)" },
            ["units.imperial"] = new[] { "Imperial (mi, gal)", "ヤードポンド法 (mi, gal)", "英制 (mi, gal)" },
            ["units.temperature"] = new[] { "Temperature", "温度", "温度" },
            ["units.celsius"] = new[] { "Celsius", "摂氏", "摄氏度" },
            ["units.fahrenheit"] = new[] { "Fahrenheit", "華氏", "华氏度" },
            ["units.pressure"] = new[] { "Pressure", "圧力", "压力" },
            ["units.consumption"] = new[] { "Consumption", "燃費単位", "油耗单位" },
            ["units.note"] = new[]
            {
                "Speed, distance and temperature update across every screen as soon as you change these.",
                "変更すると、すべての画面の速度・距離・温度が切り替わります。",
                "修改后，所有界面的速度、距离和温度会立即更新。",
            },
            ["units.reset"] = new[] { "Reset to defaults", "初期設定に戻す", "恢复默认值" },
            ["units.reset.title"] = new[] { "Reset all settings?", "設定を初期化しますか？", "确认恢复所有设置？" },
            ["units.reset.body"] = new[]
            {
                "Every preference returns to its default value. Fault codes and trip data are not affected.",
                "すべての設定が初期値に戻ります。故障コードとトリップは変わりません。",
                "所有偏好设置将恢复默认值。故障码和行程数据不受影响。",
            },
            ["units.reset.ok"] = new[] { "Reset", "初期化", "恢复" },

            ["vehicle.title"] = new[] { "Vehicle Information", "車両情報", "车辆信息" },
            ["vehicle.subtitle"] = new[] { "Read from the ECU over mode 09", "モード 09 で ECU から取得", "通过模式 09 从 ECU 读取" },
            ["vehicle.make"] = new[] { "Make", "メーカー", "品牌" },
            ["vehicle.model"] = new[] { "Model", "車名", "车型" },
            ["vehicle.year"] = new[] { "Year", "年式", "年款" },
            ["vehicle.vin"] = new[] { "VIN", "車両番号", "车架号" },
            ["vehicle.protocol"] = new[] { "OBD2 Protocol", "OBD2 プロトコル", "OBD2 协议" },
            ["vehicle.ecu"] = new[] { "ECU Version", "ECU バージョン", "ECU 版本" },
            ["vehicle.calibration"] = new[] { "Calibration ID", "キャリブレーション ID", "标定 ID" },
            ["vehicle.engine"] = new[] { "Engine", "エンジン", "发动机" },
            ["vehicle.adapter"] = new[] { "Adapter", "アダプタ", "诊断器" },

            ["about.version"] = new[] { "Version {0}", "バージョン {0}", "版本 {0}" },
            ["about.tagline"] = new[]
            {
                "Real-time vehicle monitoring and fault detection for a safer drive.",
                "安全な運転のためのリアルタイム車両監視と故障検知。",
                "实时车辆监控与故障检测，让驾驶更安全。",
            },
            ["about.build"] = new[] { "Build", "ビルド", "版本信息" },
            ["about.adapters"] = new[] { "Adapter support", "対応アダプタ", "支持诊断器" },
            ["about.protocols"] = new[] { "Protocols", "プロトコル", "支持协议" },
            ["about.shortcuts"] = new[] { "Shortcuts", "ショートカット", "快捷键" },
            ["about.shortcuts.value"] = new[]
            {
                "F1-F4 pages · F11 full screen · Ctrl+D theme · Esc back",
                "F1-F4 画面 · F11 全画面 · Ctrl+D テーマ · Esc 戻る",
                "F1-F4 页面 · F11 全屏 · Ctrl+D 主题 · Esc 返回",
            },
            ["about.copyright"] = new[] { "Copyright", "著作権", "版权" },
            ["about.exit"] = new[] { "Exit application", "アプリを終了", "退出应用" },
            ["about.exit.title"] = new[] { "Close the application?", "アプリを終了しますか？", "确认退出应用？" },
            ["about.exit.body"] = new[]
            {
                "Monitoring stops and the adapter link is released.",
                "監視を停止し、アダプタ接続を解放します。",
                "将停止监控并断开诊断器连接。",
            },
            ["about.exit.ok"] = new[] { "Exit", "終了", "退出" },
        };
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CKeyViewer.Core;

namespace CKeyViewer.Adofai;

// ══════════════════════════════════════════════════════════════════════
//  元素
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// ADOFAI 覆盖层里「可以单独摆放」的一个元素。
/// <para>
/// 对应 JipperOverlayer 的 DisplayElement —— 原版每个元素也都有自己的
/// 位置 / 对齐 / 字号 / 配色，这里按同样的粒度拆开。
/// </para>
/// </summary>
public sealed class AdofaiElement
{
    public string Id { get; set; } = "";

    /// <summary>
    /// 是否显示。取代了老配置里那几个全局 <c>ShowXxx</c> 开关 ——
    /// 每个元素一个开关比「全局开关 + 单元素开关」两层更不容易让人迷糊。
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>位置：相对工作区的 0~1 归一化坐标（0,0 = 工作区左上角）。</summary>
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.5;

    /// <summary>0 = X 指左边缘、1 = X 指中线、2 = X 指右边缘；Y 一律指顶边。</summary>
    public int Align { get; set; }

    /// <summary>字号倍率（相对全局字号）。</summary>
    public double FontScale { get; set; } = 1.0;

    /// <summary>是否用这个元素自己的颜色（否则跟随全局「文字」色）。</summary>
    public bool OwnColor { get; set; }
    public KvColor Color { get; set; } = KvColor.White;

    public void Sanitize()
    {
        Id ??= "";
        if (double.IsNaN(X)) X = 0.5;
        if (double.IsNaN(Y)) Y = 0.5;
        if (double.IsNaN(FontScale)) FontScale = 1.0;
        X = Clamp01(X);
        Y = Clamp01(Y);
        if (Align < 0 || Align > 2) Align = 0;
        FontScale = Math.Max(0.2, Math.Min(4.0, FontScale));
    }

    static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}

/// <summary>元素的固定清单与文案。</summary>
public static class AdofaiElements
{
    public const string Combo = "combo";
    public const string Title = "title";
    public const string Acc = "acc";
    public const string XAcc = "xacc";
    public const string Prog = "prog";
    public const string Bpm = "bpm";
    public const string Judge = "judge";
    public const string Stats = "stats";

    /// <summary>自动排列时的先后顺序（与原来的单块显示一致）。</summary>
    public static readonly string[] Order =
    {
        Combo, Title, Acc, XAcc, Prog, Bpm, Judge, Stats
    };

    public static string NameOf(string id)
    {
        switch (id)
        {
            case Combo: return "连击数 Combo";
            case Title: return "状态标题（Perfect Play 等）";
            case Acc: return "准确率 ACC";
            case XAcc: return "X-精准度 X-ACC";
            case Prog: return "进度 Progress";
            case Bpm: return "BPM";
            case Judge: return "判定分布（判定条）";
            case Stats: return "死亡 / 检查点 / 尝试次数";
            default: return id;
        }
    }

    public static string Describe(string id)
    {
        switch (id)
        {
            case Combo: return "当前连击数。";
            case Title: return "连击达到全 Perfect 等条件时出现的标题。";
            case Acc: return "常规准确率，按判定权重加权。";
            case XAcc: return "X-精准度，Perfect 以外的档位权重更低。";
            case Prog: return "砖块进度「当前 / 总数（百分比）」。";
            case Bpm: return "当前 BPM（来自游戏的 scrConductor）。";
            case Judge: return "各判定档位的出现次数。可以整块拖到别处独立摆放。";
            case Stats: return "死亡次数、用过的检查点数、当前是第几次尝试。";
            default: return "";
        }
    }

    /// <summary>该元素的默认配置（位置按原版单块自上而下排布）。</summary>
    public static AdofaiElement Make(string id)
    {
        var el = new AdofaiElement { Id = id };

        // 只有连击数与状态标题居中；其余一律靠左 ——
        // 靠左时各自的「标签列 + 数值列」才会竖着对成两列（原版单块就是这个观感），
        // 居中会让每一行的数值来回错位。
        switch (id)
        {
            case Combo:
                el.FontScale = 1.9;          // 连击数在原版里就是最大的那行
                el.Align = 1;
                break;
            case Title:
                el.Align = 1;
                break;
        }
        return el;
    }

    public static List<AdofaiElement> Defaults()
    {
        var list = new List<AdofaiElement>();
        foreach (string id in Order) list.Add(Make(id));
        return list;
    }
}

// ══════════════════════════════════════════════════════════════════════
//  文案
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 各行的标签文字。对应 JipperOverlayer 的 <c>LabelConfig</c> ——
/// 原版允许把 ACC / X-ACC / PROG 这些前缀改成任意文案（比如换成中文）。
/// </summary>
public sealed class AdofaiLabels
{
    public string Acc { get; set; } = "ACC";
    public string XAcc { get; set; } = "X-ACC";
    public string Prog { get; set; } = "PROG";
    public string Bpm { get; set; } = "BPM";
    public string Judge { get; set; } = "JUDGE";

    public string PerfectPlay { get; set; } = "PERFECT PLAY";
    public string Perfectionist { get; set; } = "PERFECTIONIST";
    public string AutoTile { get; set; } = "AUTO-TILE";

    /// <summary>死亡 / 检查点 / 尝试那一行的模板：{0}=死亡 {1}=检查点 {2}=尝试次数。</summary>
    public string Stats { get; set; } = "Deaths {0}   CP {1}   Try {2}";

    public void Sanitize()
    {
        if (Acc == null) Acc = "ACC";
        if (XAcc == null) XAcc = "X-ACC";
        if (Prog == null) Prog = "PROG";
        if (Bpm == null) Bpm = "BPM";
        if (Judge == null) Judge = "JUDGE";
        if (PerfectPlay == null) PerfectPlay = "PERFECT PLAY";
        if (Perfectionist == null) Perfectionist = "PERFECTIONIST";
        if (AutoTile == null) AutoTile = "AUTO-TILE";
        if (string.IsNullOrEmpty(Stats)) Stats = "Deaths {0}   CP {1}   Try {2}";
    }

    /// <summary>套用模板；用户把占位符写坏了就退回默认句式，不能让异常把整层打掉。</summary>
    public string StatsLine(int deaths, int checkpoints, int attempts)
    {
        try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, Stats, deaths, checkpoints, attempts); }
        catch { return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Deaths {0}   CP {1}   Try {2}", deaths, checkpoints, attempts); }
    }
}

// ══════════════════════════════════════════════════════════════════════
//  设置
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// ADOFAI Overlayer 的显示配置。
/// 单独存在 <c>config/adofai.json</c>，不进档案（profiles/*.json）——
/// 档案要与 jipper 原版双向兼容，多字段会破坏互读。
/// </summary>
public sealed class AdofaiSettings
{
    public bool Enabled { get; set; }

    /// <summary>元素清单。老配置里没有这个数组，读进来是空 ⇒ 由 <see cref="Sanitize"/> 播种。</summary>
    public List<AdofaiElement> Elements { get; set; } = new List<AdofaiElement>();

    /// <summary>
    /// 自动排列：把可见元素自上而下叠成一列（原来的样子）。
    /// 关掉之后每个元素各用各的 X/Y，可以随意拖到屏幕任何位置。
    /// </summary>
    public bool AutoLayout { get; set; } = true;

    /// <summary>自动排列时整块信息贴在工作区的哪个位置（<see cref="KvAnchor"/>）。</summary>
    public int SnapAnchor { get; set; } = (int)KvAnchor.TopCenter;

    /// <summary>吸附时离工作区边缘的空隙（DIP）。</summary>
    public double SnapMargin { get; set; } = 24;

    /// <summary>自由摆放时整块的锚点（自动排列 + 未吸附时生效）。</summary>
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.06;

    /// <summary>整块的对齐：0=左 1=中 2=右。</summary>
    public int Align { get; set; } = 1;

    /// <summary>
    /// 吸附到 ADOFAI 游戏窗口：开启后按键覆盖层与信息层都贴着游戏窗口摆，
    /// 游戏窗口移动 / 缩放时跟着走（带平滑动画）；游戏没运行时退回普通工作区吸附。
    /// </summary>
    public bool SnapToGame { get; set; }

    /// <summary>游戏窗口匹配串：标题包含该子串即命中（不区分大小写）；空则退回 "UnityWndClass" 类。</summary>
    public string GameWindowMatch { get; set; } = "A Dance of Fire and Ice";

    /// <summary>吸附到游戏窗口时，按键覆盖层贴游戏窗口的哪个角（<see cref="KvAnchor"/>）。</summary>
    public int KeyAnchor { get; set; } = (int)KvAnchor.BottomCenter;

    /// <summary>
    /// 信息层布局预设（自动排列开启时生效）：0 堆叠 / 1 顶栏 / 2 底栏 / 3 左侧 / 4 右侧 / 5 精简。
    /// 预设决定整块是横排还是竖排、以及默认贴在哪个边，进度条在内的元素都跟着重排。
    /// </summary>
    public int LayoutPreset { get; set; }

    /// <summary>布局预设的中文名（下标即 <see cref="LayoutPreset"/>）。</summary>
    public static readonly string[] LayoutPresetNames =
    {
        "堆叠（竖排）", "顶栏（横排）", "底栏（横排）", "左侧栏", "右侧栏", "精简",
    };

    public double FontSize { get; set; } = 22;
    public bool Bold { get; set; } = true;
    public bool Italic { get; set; }

    /// <summary>空表示跟随 KV 当前字体。</summary>
    public string FontRef { get; set; } = "";

    /// <summary>各行的标签文案（JipperOverlayer 的 LabelConfig）。</summary>
    public AdofaiLabels Labels { get; set; } = new AdofaiLabels();

    public KvColor Color { get; set; } = KvColor.White;
    public KvColor LabelColor { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.55f);

    /// <summary>连击标题（Perfect Play / Perfectionist / Auto-tile）用单独配色。</summary>
    public KvColor TitleColor { get; set; } = KvColor.Rgba(1f, 0.85f, 0.3f, 1f);

    // ---- 老配置的全局开关：只作为一次性迁移的输入，界面已经改用元素自己的 Visible ----

    public bool ShowCombo { get; set; } = true;
    public bool ShowTitle { get; set; } = true;
    public bool ShowAccuracy { get; set; } = true;
    public bool ShowXAccuracy { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
        public bool ShowBpm { get; set; } = true;
        public bool ShowJudgement { get; set; } = true;
        public bool ShowStats { get; set; } = true;

        /// <summary>PROG 元素是否额外画一条视觉进度条（而不只是文字）。</summary>
        public bool ShowProgressBar { get; set; } = true;

        /// <summary>进度条填充色（默认用一抹青绿，跟信息层主色区分开）。</summary>
        public KvColor ProgressBarColor { get; set; } = KvColor.Rgba(0.27f, 0.95f, 0.55f, 1f);

        /// <summary>进度条轨道底色（半透明白）。</summary>
        public KvColor ProgressBarTrack { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.16f);

    /// <summary>连击是否把 Early/Late Perfect 算进去。</summary>
    public bool AllowElCombo { get; set; } = true;
    /// <summary>连击是否把 Auto（自动砖）算进去。</summary>
    public bool AllowAutoCombo { get; set; } = true;

    /// <summary>行间距（相对字号的比例）。</summary>
    public double LineGap { get; set; } = 1.35;

    public bool HasOutline { get; set; } = true;
    public KvColor OutlineColor { get; set; } = KvColor.Black;
    public double OutlineWidth { get; set; } = 0.06;

    /// <summary>布局模式：即使没连上游戏也把元素画出来，方便先把位置摆好。</summary>
    public bool ShowInLayoutMode { get; set; } = true;

    /// <summary>把老配置的全局 ShowXxx 搬到元素自己的 Visible 上（只在第一次做）。</summary>
    void SeedElements()
    {
        Elements = AdofaiElements.Defaults();

        void Off(string id)
        {
            var el = Find(id);
            if (el != null) el.Visible = false;
        }

        if (!ShowCombo) Off(AdofaiElements.Combo);
        if (!ShowTitle) Off(AdofaiElements.Title);
        if (!ShowAccuracy) Off(AdofaiElements.Acc);
        if (!ShowXAccuracy) Off(AdofaiElements.XAcc);
        if (!ShowProgress) Off(AdofaiElements.Prog);
        if (!ShowBpm) Off(AdofaiElements.Bpm);
        if (!ShowJudgement) Off(AdofaiElements.Judge);
        if (!ShowStats) Off(AdofaiElements.Stats);
    }

    public AdofaiElement Find(string id)
    {
        if (Elements == null) return null;
        for (int i = 0; i < Elements.Count; i++)
            if (Elements[i] != null && Elements[i].Id == id) return Elements[i];
        return null;
    }

    /// <summary>取元素配置；老配置里缺失的就地补一个，保证界面永远拿得到对象。</summary>
    public AdofaiElement ElementOf(string id)
    {
        var el = Find(id);
        if (el != null) return el;
        el = AdofaiElements.Make(id);
        Elements ??= new List<AdofaiElement>();
        Elements.Add(el);
        return el;
    }

    /// <summary>按元素清单顺序返回（忽略清单里不认识的 id）。</summary>
    public List<AdofaiElement> OrderedElements()
    {
        var list = new List<AdofaiElement>();
        foreach (string id in AdofaiElements.Order) list.Add(ElementOf(id));
        return list;
    }

    public void Sanitize()
    {
        if (Elements == null || Elements.Count == 0) SeedElements();
        Elements.RemoveAll(e => e == null);
        for (int i = 0; i < Elements.Count; i++) Elements[i].Sanitize();
        foreach (string id in AdofaiElements.Order) ElementOf(id);   // 补齐缺项

        if (double.IsNaN(X)) X = 0.5;
        if (double.IsNaN(Y)) Y = 0.06;
        X = Clamp01(X);
        Y = Clamp01(Y);
        if (Align < 0 || Align > 2) Align = 1;

        SnapAnchor = KvSnap.Clamp(SnapAnchor);
        if (double.IsNaN(SnapMargin)) SnapMargin = 24;
        SnapMargin = Math.Max(0, Math.Min(400, SnapMargin));
        KeyAnchor = KvSnap.Clamp(KeyAnchor);
        GameWindowMatch ??= "A Dance of Fire and Ice";
        if (LayoutPreset < 0) LayoutPreset = 0;
        if (LayoutPreset >= LayoutPresetNames.Length) LayoutPreset = 0;

        FontSize = Math.Max(4, Math.Min(400, FontSize));
        if (double.IsNaN(LineGap)) LineGap = 1.35;
        LineGap = Math.Max(0.6, Math.Min(4, LineGap));
        if (double.IsNaN(OutlineWidth)) OutlineWidth = 0.06;
        OutlineWidth = Math.Max(0, Math.Min(0.5, OutlineWidth));
        FontRef ??= "";
        Labels ??= new AdofaiLabels();
        Labels.Sanitize();
    }

    static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}

// ══════════════════════════════════════════════════════════════════════
//  绘制
// ══════════════════════════════════════════════════════════════════════

/// <summary>画完之后留下的元素矩形（窗口本地坐标），供命中测试与拖动使用。</summary>
public struct AdofaiBox
{
    public string Id;
    public Rect Rect;
}

/// <summary>
/// 把 <see cref="AdofaiState"/> 画到 ADOFAI 自己的覆盖层窗口上。
/// <para>
/// 与键帽渲染彻底分开：这里画在一块铺满工作区的透明窗口上，
/// 所以元素能被拖到屏幕的任何位置（不受按键覆盖层那块小窗口的限制）。
/// </para>
/// </summary>
public sealed class AdofaiOverlay
{
    public AdofaiSettings Settings = new AdofaiSettings();
    public AdofaiState State = new AdofaiState();

    /// <summary>是否已连上游戏且在关卡中。</summary>
    public bool Visible;

    /// <summary>布局模式（由 KvHost 打开）：没连游戏也画出来，并且可以拖动。</summary>
    public bool LayoutMode;

    /// <summary>布局模式下高亮的元素 id。</summary>
    public string Selected;
    public string Hovered;

    /// <summary>上一帧各元素的矩形。</summary>
    public readonly List<AdofaiBox> Boxes = new List<AdofaiBox>();

    /// <summary>上一帧整块信息的包围盒（空表示没画）。</summary>
    public Rect GroupRect { get; private set; }
    public bool HasContent { get; private set; }

    /// <summary>上一帧的绘制区域（= 窗口本地坐标下的工作区）。</summary>
    public Rect Area { get; private set; }

    readonly List<Item> _items = new List<Item>();

    // ---------------------------------------------------------------
    // 元素文本
    // ---------------------------------------------------------------

    sealed class Item
    {
        public AdofaiElement El;
        public readonly List<Line> Lines = new List<Line>();
        public double Width, Height;
        public double X, Y;
        public double InnerGap;

        /// <summary>该元素是不是 PROG（进度）。进度条只画在它下面。</summary>
        public bool IsProg;
        /// <summary>是否要画进度条（ShowProgressBar 且元素是 PROG）。</summary>
        public bool ShowBar;
        /// <summary>进度条高度（DIP，按字号缩放）。</summary>
        public double BarH;

        public void Measure(double innerGap)
        {
            InnerGap = innerGap;

            double labelW = 0;
            Height = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                var ln = Lines[i];
                ln.LabelOffset = 0;
                if (ln.Label != null && ln.Label.Width > labelW) labelW = ln.Label.Width;
                Height += Math.Max(ln.Label?.Height ?? 0, ln.Value.Height);
                if (i < Lines.Count - 1) Height += innerGap;
            }
            double gap = labelW > 0 ? Math.Max(2, innerGap * 0.34) : 0;
            Width = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                var ln = Lines[i];
                ln.LabelOffset = labelW - (ln.Label?.Width ?? 0);
                double w = labelW + (ln.Label != null ? gap : 0) + ln.Value.Width;
                if (w > Width) Width = w;
                ln.ValueOffset = labelW > 0 && ln.Label != null ? labelW + gap : 0;
            }

            if (ShowBar)
            {
                BarH = Math.Max(3, innerGap * 0.9);
                Height += innerGap + BarH;   // 文字与进度条之间留一点缝
            }
        }
    }

    sealed class Line
    {
        public FormattedText Label;
        public FormattedText Value;
        public bool IsTitle;
        public double LabelOffset;
        public double ValueOffset;
    }

    /// <summary>判定档位显示名，下标与 <see cref="AdofaiHitMargin"/> 一致。</summary>
    static readonly string[] MarginNames =
    {
        "Too Early", "Very Early", "Early Perfect", "Perfect", "Late Perfect", "Very Late",
        "Too Late", "Multipress", "Miss", "Overload", "Auto", "Overpress"
    };

    static string TitleText(AdofaiComboTitle t, AdofaiLabels lab)
    {
        switch (t)
        {
            case AdofaiComboTitle.PerfectPlay: return lab.PerfectPlay;
            case AdofaiComboTitle.Perfectionist: return lab.Perfectionist;
            case AdofaiComboTitle.AutoTile: return lab.AutoTile;
            default: return "";
        }
    }

    static string Pct(float v) => (v * 100f).ToString("F2", CultureInfo.InvariantCulture) + "%";
    static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>把每个可见元素要显示的文字收集起来（顺序 = <see cref="AdofaiElements.Order"/>）。</summary>
    Dictionary<string, List<(string label, string value, bool title)>> BuildText()
    {
        var map = new Dictionary<string, List<(string, string, bool)>>();
        var s = State;
        var lab = Settings.Labels;

        map[AdofaiElements.Combo] = new List<(string, string, bool)>
        {
            ("", Num(s.Combo), false)
        };

        if (s.ComboTitle != AdofaiComboTitle.None)
            map[AdofaiElements.Title] = new List<(string, string, bool)>
            {
                ("", TitleText(s.ComboTitle, lab), true)
            };

        map[AdofaiElements.Acc] = new List<(string, string, bool)> { (lab.Acc, Pct(s.Accuracy), false) };
        map[AdofaiElements.XAcc] = new List<(string, string, bool)> { (lab.XAcc, Pct(s.XAccuracy), false) };

        string prog = s.TotalTiles > 0
            ? $"{s.CurrentTile} / {s.TotalTiles}  ({Pct(s.Progress)})"
            : Num(s.CurrentTile);
        map[AdofaiElements.Prog] = new List<(string, string, bool)> { (lab.Prog, prog, false) };

        map[AdofaiElements.Bpm] = new List<(string, string, bool)>
        {
            (lab.Bpm, s.Bpm.ToString("F0", CultureInfo.InvariantCulture), false)
        };

        // 判定分布：一档一行、只列出现过的档位 —— 列全 12 档时有 11 行是 0，
        // 覆盖层会被撑得很长。
        var judge = new List<(string, string, bool)>();
        int total = s.JudgedTiles;
        if (total > 0)
        {
            judge.Add((lab.Judge, Num(total), false));
            for (int i = 0; i < MarginNames.Length; i++)
            {
                if (s.HitCounts[i] == 0) continue;
                judge.Add((MarginNames[i], Num(s.HitCounts[i]), false));
            }
        }
        map[AdofaiElements.Judge] = judge;

        map[AdofaiElements.Stats] = new List<(string, string, bool)>
        {
            ("", lab.StatsLine(s.Deaths, s.Checkpoints, s.Attempts), false)
        };

        return map;
    }

    List<Item> BuildItems(Typeface tf, double ppd, double baseSize)
    {
        _items.Clear();

        var st = Settings;
        var text = BuildText();

        var fill = st.Color.ToBrush();
        var labelFill = st.LabelColor.ToBrush();
        var titleFill = st.TitleColor.ToBrush();

        foreach (string id in AdofaiElements.Order)
        {
            var el = st.ElementOf(id);
            el.Sanitize();
            if (!el.Visible) continue;
            if (!text.TryGetValue(id, out var rows) || rows.Count == 0) continue;

            double fs = Math.Max(3, baseSize * el.FontScale);
            var item = new Item { El = el, IsProg = id == AdofaiElements.Prog };
            item.ShowBar = item.IsProg && Settings.ShowProgressBar;

            foreach (var (label, value, isTitle) in rows)
            {
                var brush = isTitle ? titleFill
                          : el.OwnColor ? el.Color.ToBrush()
                          : fill;

                item.Lines.Add(new Line
                {
                    IsTitle = isTitle,
                    Value = new FormattedText(value, CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, tf, fs, brush, ppd),
                    Label = string.IsNullOrEmpty(label) ? null
                        : new FormattedText(label, CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, tf, fs,
                            isTitle ? titleFill : labelFill, ppd),
                });
            }

            if (item.Lines.Count > 0) _items.Add(item);
        }

        return _items;
    }

    // ---------------------------------------------------------------
    // 绘制
    // ---------------------------------------------------------------

    public void Draw(DrawingContext dc, Rect area, Typeface tf, double ppd, double scale)
    {
        Reset();
        Area = area;

        var st = Settings;
        if (!st.Enabled) return;
        if (!Visible && !(LayoutMode && st.ShowInLayoutMode)) return;

        double baseSize = Math.Max(4, st.FontSize * scale);
        double gapPx = Math.Max(1.0, st.LineGap) * baseSize;

        var items = BuildItems(tf, ppd, baseSize);
        if (items.Count == 0) return;

        // 量一遍尺寸
        double groupW = 0, groupH = 0;
        foreach (var it in items)
        {
            it.Measure(baseSize * Math.Max(0.6, st.LineGap));
            if (it.Width > groupW) groupW = it.Width;
            groupH += it.Height;
        }
        groupH += gapPx * (items.Count - 1);

        // 工作区在窗口本地坐标里就是 (0,0,W,H) —— 窗口本身铺满工作区
        var local = new Rect(0, 0, area.Width, area.Height);

        if (st.AutoLayout)
        {
            int preset = st.LayoutPreset;
            bool horizontal = preset == 1 || preset == 2 || preset == 5;
            int anchor = preset switch
            {
                1 => (int)KvAnchor.TopCenter,
                2 => (int)KvAnchor.BottomCenter,
                3 => (int)KvAnchor.MiddleLeft,
                4 => (int)KvAnchor.MiddleRight,
                5 => (int)KvAnchor.BottomCenter,
                _ => st.SnapAnchor,
            };
            // 精简：只留核心几行（其余被用户单独关掉的也不出现，因为 items 已按 Visible 过滤）
            List<Item> shown = items;
            if (preset == 5)
            {
                var core = new HashSet<string>
                {
                    AdofaiElements.Combo, AdofaiElements.Acc, AdofaiElements.XAcc, AdofaiElements.Prog
                };
                var filtered = new List<Item>();
                foreach (var it in items)
                    if (core.Contains(it.El.Id)) filtered.Add(it);
                if (filtered.Count > 0) shown = filtered;
            }
            // 左侧 / 右侧栏：列内统一靠边，避免各行标签左中右乱跳
            int forceAlign = preset == 3 ? 0 : preset == 4 ? 2 : -1;

            if (horizontal) LayoutRow(shown, local, anchor, gapPx);
            else LayoutStacked(shown, local, anchor, gapPx, forceAlign);
        }
        else LayoutFree(items, local);

        Pen outline = st.HasOutline && st.OutlineWidth > 0
            ? new Pen(st.OutlineColor.ToBrush(), Math.Max(0.5, st.OutlineWidth * baseSize))
            : null;

        var rect = Rect.Empty;
        foreach (var it in items)
        {
            DrawItem(dc, it, outline);
            Boxes.Add(new AdofaiBox { Id = it.El.Id, Rect = new Rect(it.X, it.Y, it.Width, it.Height) });
            rect = rect.IsEmpty ? Boxes[Boxes.Count - 1].Rect : Rect.Union(rect, Boxes[Boxes.Count - 1].Rect);
        }

        GroupRect = rect;
        HasContent = true;

        if (LayoutMode) DrawLayoutMarks(dc, rect, local, baseSize);
    }

    void Reset()
    {
        Boxes.Clear();
        _items.Clear();
        GroupRect = Rect.Empty;
        HasContent = false;
    }

    /// <summary>自动排列（竖排）：整块自上而下叠成一列，列内每个元素用各自的对齐。</summary>
    void LayoutStacked(List<Item> items, Rect area, int anchor, double gapPx, int forceAlign)
    {
        var st = Settings;

        double groupW = 0, groupH = 0;
        foreach (var it in items)
        {
            if (it.Width > groupW) groupW = it.Width;
            groupH += it.Height;
        }
        groupH += gapPx * Math.Max(0, items.Count - 1);

        if (!KvSnap.Place(anchor, st.SnapMargin, groupW, groupH, area, out double left, out double top))
        {
            // 没吸附：整块的位置由 X/Y + Align 决定（Align 指的是 X 落在块的哪条边）
            int ba = Math.Clamp(st.Align, 0, 2);
            left = st.X * area.Width - (ba == 1 ? groupW * 0.5 : ba == 2 ? groupW : 0);
            top = st.Y * area.Height;
        }

        double y = top;
        foreach (var it in items)
        {
            // 列内位置用元素自己的 Align（被预设强制靠边时用 forceAlign）——
            // 都靠左时「标签列 + 数值列」才竖着对成两列。
            int a = forceAlign >= 0 ? forceAlign : Math.Clamp(it.El.Align, 0, 2);
            it.X = a == 0 ? left
                 : a == 1 ? left + (groupW - it.Width) * 0.5
                          : left + groupW - it.Width;
            it.Y = y;
            y += it.Height + gapPx;
        }
    }

    /// <summary>
    /// 自动排列（横排）：整块从左到右排成一行（顶栏 / 底栏 / 精简布局用）。
    /// 每行高度取所有元素里最高的那个，元素之间留同样的缝。
    /// </summary>
    void LayoutRow(List<Item> items, Rect area, int anchor, double gapPx)
    {
        var st = Settings;

        double groupW = 0, groupH = 0;
        foreach (var it in items)
        {
            groupW += it.Width;
            if (it.Height > groupH) groupH = it.Height;
        }
        groupW += gapPx * Math.Max(0, items.Count - 1);

        if (!KvSnap.Place(anchor, st.SnapMargin, groupW, groupH, area, out double left, out double top))
        {
            int ba = Math.Clamp(st.Align, 0, 2);
            left = st.X * area.Width - (ba == 1 ? groupW * 0.5 : ba == 2 ? groupW : 0);
            top = st.Y * area.Height;
        }

        double x = left;
        foreach (var it in items)
        {
            it.X = x;
            it.Y = top;
            x += it.Width + gapPx;
        }
    }

    /// <summary>自由摆放：每个元素用各自的 X/Y / Align。</summary>
    static void LayoutFree(List<Item> items, Rect area)
    {
        foreach (var it in items)
        {
            var el = it.El;
            int a = Math.Clamp(el.Align, 0, 2);
            it.X = el.X * area.Width - (a == 1 ? it.Width * 0.5 : a == 2 ? it.Width : 0);
            it.Y = el.Y * area.Height;
        }
    }

    void DrawItem(DrawingContext dc, Item it, Pen outline)
    {
        double y = it.Y;

        foreach (var ln in it.Lines)
        {
            double h = Math.Max(ln.Label?.Height ?? 0, ln.Value.Height);

            if (ln.Label != null)
                DrawText(dc, ln.Label, new Point(it.X + ln.LabelOffset, y), outline);

            DrawText(dc, ln.Value, new Point(it.X + ln.ValueOffset, y), outline);

            y += h + it.InnerGap;
        }

        // 进度条（仅 PROG 元素）：轨道 + 填充
        if (it.ShowBar)
        {
            double ratio = Math.Max(0, Math.Min(1, State.Progress));
            double h = it.BarH;
            double r = h * 0.5;
            var trackRect = new Rect(it.X, y, it.Width, h);
            dc.DrawRoundedRectangle(Settings.ProgressBarTrack.ToBrush(), null, trackRect, r, r);

            double fillW = it.Width * ratio;
            if (fillW > 0.5)
            {
                var fillRect = new Rect(it.X, y, fillW, h);
                dc.DrawRoundedRectangle(Settings.ProgressBarColor.ToBrush(), null, fillRect, r, r);
            }
        }
    }

    static void DrawText(DrawingContext dc, FormattedText ft, Point p, Pen outline)
    {
        if (outline != null) dc.DrawGeometry(null, outline, ft.BuildGeometry(p));
        dc.DrawText(ft, p);
    }

    /// <summary>布局模式下的提示：选中 / 悬停元素的虚线框，外加一句操作说明。</summary>
    void DrawLayoutMarks(DrawingContext dc, Rect rect, Rect area, double baseSize)
    {
        var dash = new Pen(new SolidColorBrush(Color.FromArgb(200, 10, 132, 255)), 1.5)
        {
            DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
        };
        var hover = new Pen(new SolidColorBrush(Color.FromArgb(120, 10, 132, 255)), 1)
        {
            DashStyle = new DashStyle(new double[] { 3, 3 }, 0)
        };

        for (int i = 0; i < Boxes.Count; i++)
        {
            var b = Boxes[i];
            var r = Rect.Inflate(b.Rect, 2, 2);
            if (b.Id == Selected) dc.DrawRectangle(null, dash, r);
            else if (b.Id == Hovered) dc.DrawRectangle(null, hover, r);
        }

        var hint = new FormattedText(
            "拖动元素可单独摆放 · 拖空白处或按住 Shift 拖动整体 · Esc 退出",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), Math.Max(11, baseSize * 0.62),
            new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)), 1.0);

        double hy = rect.Top - hint.Height - 8;
        if (hy < area.Top + 2) hy = rect.Bottom + 6;
        if (hy + hint.Height > area.Bottom) hy = Math.Max(area.Top + 2, area.Bottom - hint.Height - 4);
        double hx = Math.Max(area.Left + 4, Math.Min(rect.Left, area.Right - hint.Width - 4));

        var box = new Rect(hx - 6, hy - 3, hint.Width + 12, hint.Height + 6);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 28, 28, 30)),
            new Pen(new SolidColorBrush(Color.FromArgb(160, 10, 132, 255)), 1), box, 6, 6);
        dc.DrawText(hint, new Point(hx, hy));
    }

    // ---------------------------------------------------------------
    // 命中测试 / 拖动
    // ---------------------------------------------------------------

    /// <summary>命中最上面的元素（返回 null 表示没压在任何元素上）。</summary>
    public AdofaiElement HitTestElement(Point p)
    {
        for (int i = Boxes.Count - 1; i >= 0; i--)
        {
            if (!Boxes[i].Rect.Contains(p)) continue;
            return Settings.Find(Boxes[i].Id);
        }
        return null;
    }

    public bool HitTestGroup(Point p) => GroupRect.Contains(p);

    /// <summary>某个元素上一帧的矩形（没有则空）。</summary>
    public Rect RectOf(string id)
    {
        for (int i = 0; i < Boxes.Count; i++)
            if (Boxes[i].Id == id) return Boxes[i].Rect;
        return Rect.Empty;
    }

    /// <summary>
    /// 把自动排列的当前结果写进各元素自己的 X/Y，然后退出自动排列。
    /// 拖动任何一个元素前都会先调它 —— 这样「把判定条拖走」时其余元素留在原地不动。
    /// </summary>
    public bool DetachAutoLayout()
    {
        var area = Area;
        if (!Settings.AutoLayout || area.Width <= 0 || area.Height <= 0) return false;

        foreach (var it in _items)
        {
            var el = it.El;
            int a = Math.Clamp(el.Align, 0, 2);
            double anchorX = a == 1 ? it.X + it.Width * 0.5 : a == 2 ? it.X + it.Width : it.X;
            el.X = Math.Max(0, Math.Min(1, anchorX / area.Width));
            el.Y = Math.Max(0, Math.Min(1, it.Y / area.Height));
        }

        Settings.AutoLayout = false;
        return true;
    }

    /// <summary>设置面板里点「独立摆放」时用：先记下当前位置再脱离自动排列。</summary>
    public bool DetachFromAutoForEditor() => DetachAutoLayout();

    /// <summary>
    /// 按归一化增量移动单个元素（自动夹在工作区内，保证整块不跑出屏幕）。
    /// </summary>
    public bool MoveElement(AdofaiElement el, double dx, double dy, Rect area)
    {
        if (el == null || area.Width <= 0 || area.Height <= 0) return false;

        var r = RectOf(el.Id);
        double w = r.IsEmpty ? 0 : r.Width;
        double h = r.IsEmpty ? 0 : r.Height;
        int a = Math.Clamp(el.Align, 0, 2);

        double nx = el.X + dx / area.Width;
        double ny = el.Y + dy / area.Height;

        // X 指的是哪条边由 Align 决定，夹取范围也随之下移 / 上移半个宽度
        double minX = a == 0 ? 0 : a == 1 ? w * 0.5 / area.Width : w / area.Width;
        double maxX = a == 0 ? 1 - w / area.Width : a == 1 ? 1 - w * 0.5 / area.Width : 1;
        double maxY = h > area.Height ? 0 : 1 - h / area.Height;

        double cx = Math.Max(0, Math.Min(1, Math.Max(minX, Math.Min(maxX, nx))));
        double cy = Math.Max(0, Math.Min(1, Math.Max(0, Math.Min(maxY, ny))));

        if (Math.Abs(cx - el.X) < 1e-9 && Math.Abs(cy - el.Y) < 1e-9) return false;
        el.X = cx;
        el.Y = cy;
        return true;
    }

    /// <summary>按归一化增量移动自动排列的整块信息；会顺手关掉吸附（位置变成手设）。</summary>
    public bool MoveGroup(double dx, double dy, Rect area)
    {
        if (area.Width <= 0 || area.Height <= 0) return false;

        if (Settings.SnapAnchor != (int)KvAnchor.Free)
        {
            Settings.SnapAnchor = (int)KvAnchor.Free;
            // 从吸附位置接管：把当前区块的锚点换算成 X/Y
            var a = Math.Clamp(Settings.Align, 0, 2);
            double anchorX = a == 1 ? GroupRect.Left + GroupRect.Width * 0.5
                           : a == 2 ? GroupRect.Right : GroupRect.Left;
            Settings.X = Math.Max(0, Math.Min(1, anchorX / area.Width));
            Settings.Y = Math.Max(0, Math.Min(1, GroupRect.Top / area.Height));
        }

        double nx = Settings.X + dx / area.Width;
        double ny = Settings.Y + dy / area.Height;
        nx = Math.Max(0, Math.Min(1, nx));
        ny = Math.Max(0, Math.Min(1, ny));

        if (Math.Abs(nx - Settings.X) < 1e-9 && Math.Abs(ny - Settings.Y) < 1e-9) return false;
        Settings.X = nx;
        Settings.Y = ny;
        return true;
    }

    /// <summary>把每个元素弹回自动排列。</summary>
    public void ResetToAutoLayout()
    {
        Settings.AutoLayout = true;
    }

    /// <summary>
    /// 自由摆放模式下平移全部元素（拖「整体」时用）。
    /// 用每个元素各自的允许范围夹取，取所有元素里最保守的那个位移量。
    /// </summary>
    public bool MoveAll(double dx, double dy, Rect area)
    {
        if (area.Width <= 0 || area.Height <= 0) return false;

        double bestDx = dx, bestDy = dy;
        bool any = false;

        foreach (var it in _items)
        {
            var el = it.El;
            int a = Math.Clamp(el.Align, 0, 2);
            double w = it.Width, h = it.Height;

            double cur = el.X * area.Width - (a == 1 ? w * 0.5 : a == 2 ? w : 0);
            double want = cur + dx;
            double minLeft = 0;
            double maxLeft = Math.Max(0, area.Width - w);
            double clamped = Math.Max(minLeft, Math.Min(maxLeft, want));
            double allowDx = clamped - cur;

            double wantY = el.Y * area.Height + dy;
            double clampedY = Math.Max(0, Math.Min(Math.Max(0, area.Height - h), wantY));
            double allowDy = clampedY - el.Y * area.Height;

            if (!any)
            {
                bestDx = allowDx; bestDy = allowDy; any = true;
            }
            else
            {
                if (Math.Abs(allowDx) < Math.Abs(bestDx)) bestDx = allowDx;
                if (Math.Abs(allowDy) < Math.Abs(bestDy)) bestDy = allowDy;
            }
        }

        if (!any) return false;
        if (Math.Abs(bestDx) < 1e-9 && Math.Abs(bestDy) < 1e-9) return false;

        foreach (var it in _items)
        {
            it.El.X = Math.Max(0, Math.Min(1, it.El.X + bestDx / area.Width));
            it.El.Y = Math.Max(0, Math.Min(1, it.El.Y + bestDy / area.Height));
        }
        return true;
    }

    /// <summary>元素文字签名 —— 内容没变就不必重绘。</summary>
    public string TextSignature()
    {
        var sb = new System.Text.StringBuilder(128);
        sb.Append(Settings.AutoLayout ? 'A' : 'F');
        sb.Append('|').Append(Settings.LayoutPreset);
        sb.Append('|').Append(Settings.SnapAnchor).Append('|').Append(Settings.X.ToString("F4"));
        sb.Append('|').Append(Settings.Y.ToString("F4")).Append('|').Append(Settings.Align);
        sb.Append('|').Append(LayoutMode ? 1 : 0).Append('|').Append(Visible ? 1 : 0);
        sb.Append('|').Append(Settings.FontSize).Append('|').Append(Settings.LineGap);
        sb.Append('|').Append(Settings.ShowProgressBar ? 1 : 0)
          .Append('|').Append(Settings.ProgressBarColor.ToArgb().ToString("X8"))
          .Append('|').Append(Settings.ProgressBarTrack.ToArgb().ToString("X8"));
        sb.Append('|').Append(Selected ?? "").Append('|').Append(Hovered ?? "");

        foreach (string id in AdofaiElements.Order)
        {
            var el = Settings.ElementOf(id);
            sb.Append('|').Append(id).Append(el.Visible ? '1' : '0')
              .Append(el.FontScale.ToString("F2"))
              .Append(el.X.ToString("F3")).Append(el.Y.ToString("F3")).Append(el.Align);
        }

        var s = State;
        sb.Append('#').Append(s.InLevel).Append(',').Append(s.Combo).Append(',').Append(s.ComboTitle)
          .Append(',').Append(s.CurrentTile).Append(',').Append(s.TotalTiles)
          .Append(',').Append(s.Accuracy.ToString("F4")).Append(',').Append(s.XAccuracy.ToString("F4"))
          .Append(',').Append(s.Progress.ToString("F4")).Append(',').Append(s.Bpm.ToString("F1"))
          .Append(',').Append(s.Deaths).Append(',').Append(s.Checkpoints).Append(',').Append(s.Attempts);
        for (int i = 0; i < 12; i++) sb.Append(',').Append(s.HitCounts[i]);

        return sb.ToString();
    }
}

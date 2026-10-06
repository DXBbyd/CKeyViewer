namespace CKeyViewer.Ui
{
    /// <summary>
    /// 一套界面配色。取值刻意照抄 iOS 的系统色（App Store 那套）：
    /// systemGroupedBackground / secondarySystemGroupedBackground / label / secondaryLabel /
    /// systemBlue / systemRed / separator……
    ///
    /// 之所以用 iOS 的语义色常量而不是随手挑颜色：这样浅色与深色两套能保持同样的对比关系，
    /// 切过去不会出现「深色下能看清、浅色下糊成一片」。
    /// </summary>
    public sealed class KvPalette
    {
        /// <summary>是否深色。</summary>
        public bool IsDark { get; private set; }

        /// <summary>窗口底色（iOS systemGroupedBackground）。</summary>
        public string Bg { get; private set; }

        /// <summary>分组卡片底色（iOS secondarySystemGroupedBackground）。</summary>
        public string Card { get; private set; }

        /// <summary>卡片内的次级填充（输入框 / 小按钮底）。</summary>
        public string CardAlt { get; private set; }

        /// <summary>侧边栏底色。</summary>
        public string Sidebar { get; private set; }

        /// <summary>控件描边。</summary>
        public string Border { get; private set; }

        /// <summary>行之间的细分隔线（比 Border 更淡）。</summary>
        public string Separator { get; private set; }

        /// <summary>主文字（label）。</summary>
        public string Text { get; private set; }

        /// <summary>次要文字（secondaryLabel）。</summary>
        public string Sub { get; private set; }

        /// <summary>强调色（systemBlue）。</summary>
        public string Accent { get; private set; }

        /// <summary>强调色的淡底（选中项背景）。</summary>
        public string AccentSoft { get; private set; }

        /// <summary>悬停底色。</summary>
        public string Hover { get; private set; }

        /// <summary>危险色（systemRed，删除类按钮）。</summary>
        public string Danger { get; private set; }

        /// <summary>危险色的淡底。</summary>
        public string DangerSoft { get; private set; }

        /// <summary>开关 / 滑轨的「关」底色。</summary>
        public string SwitchOff { get; private set; }

        /// <summary>分段控件里「选中那一块」的底色（iOS 里比轨道亮一档）。</summary>
        public string SegmentOn { get; private set; }

        // ---------------------------------------------------------------

        /// <summary>浅色 —— 对应 iOS 的 Light 外观。</summary>
        public static readonly KvPalette Light = new KvPalette
        {
            IsDark = false,
            Bg = "#F2F2F7",
            Card = "#FFFFFF",
            CardAlt = "#F2F2F7",
            Sidebar = "#FFFFFF",
            Border = "#D8D8DC",
            Separator = "#DDDDE2",
            Text = "#1C1C1E",
            Sub = "#8E8E93",
            Accent = "#007AFF",
            AccentSoft = "#E1EFFF",
            Hover = "#F0F0F5",
            Danger = "#FF3B30",
            DangerSoft = "#FFE5E3",
            SwitchOff = "#E9E9EB",
            SegmentOn = "#FFFFFF",
        };

        /// <summary>深色 —— 对应 iOS 的 Dark 外观。</summary>
        public static readonly KvPalette Dark = new KvPalette
        {
            IsDark = true,
            Bg = "#000000",
            Card = "#1C1C1E",
            CardAlt = "#2C2C2E",
            Sidebar = "#1C1C1E",
            Border = "#38383A",
            Separator = "#3A3A3C",
            Text = "#FFFFFF",
            Sub = "#8E8E93",
            Accent = "#0A84FF",
            AccentSoft = "#12385F",
            Hover = "#2C2C2E",
            Danger = "#FF453A",
            DangerSoft = "#4A1F1C",
            SwitchOff = "#39393D",
            SegmentOn = "#636366",
        };

        /// <summary>按 <c>"light"</c> / <c>"dark"</c> 取调色板（其它值默认深色）。</summary>
        public static KvPalette For(string name) =>
            string.Equals(name, "light", System.StringComparison.OrdinalIgnoreCase) ? Light : Dark;

        public static string NameOf(bool dark) => dark ? "dark" : "light";
    }
}

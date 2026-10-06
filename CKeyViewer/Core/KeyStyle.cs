namespace CKeyViewer.Core
{
    /// <summary>
    /// 单个键槽最终生效的配色与样式。由 <c>KvHost</c> 预解析好放进渲染器，
    /// 渲染时只做查表，避免每帧重复判断「是否启用每键配色 / 全键盘统一配色 / 节点自定义配色」。
    /// </summary>
    public struct KeyStyle
    {
        public KvColor Background;
        public KvColor BackgroundClicked;
        public KvColor Outline;
        public KvColor OutlineClicked;
        public KvColor Text;
        public KvColor TextClicked;

        /// <summary>0 表示沿用全局字号。</summary>
        public double FontSize;

        /// <summary>是否显示该键下方的点击次数（脚键为 false）。</summary>
        public bool ShowCount;

        /// <summary>是否隐藏键名（自由布局节点的 HideLabel）。</summary>
        public bool HideLabel;

        /// <summary>不透明度 0~1；&lt;0 表示沿用默认（1）。</summary>
        public double Opacity;

        /// <summary>圆角半径（参考单位）；&lt;0 表示沿用全局默认。</summary>
        public double CornerRadius;

        /// <summary>边框宽度（参考单位）；&lt;0 表示沿用全局默认。</summary>
        public double BorderThickness;

        public KvColor Bg(bool pressed) => pressed ? BackgroundClicked : Background;
        public KvColor Line(bool pressed) => pressed ? OutlineClicked : Outline;
        public KvColor Fg(bool pressed) => pressed ? TextClicked : Text;

        public static KeyStyle From(KvColor bg, KvColor bgc, KvColor ol, KvColor olc, KvColor fg, KvColor fgc)
        {
            return new KeyStyle
            {
                Background = bg,
                BackgroundClicked = bgc,
                Outline = ol,
                OutlineClicked = olc,
                Text = fg,
                TextClicked = fgc,
                FontSize = 0,
                ShowCount = true,
                HideLabel = false,
                Opacity = -1,
                CornerRadius = -1,
                BorderThickness = -1
            };
        }
    }
}

using CKeyViewer.Core;

namespace CKeyViewer.Render
{
    /// <summary>配色集合。字段命名与 jipper 配置一一对应，便于直接映射。</summary>
    public sealed class KvTheme
    {
        // 主键
        public KvColor Background = KvColor.Rgba(0.5607843f, 0.2352941f, 1.0f, 0.1960784f);
        public KvColor BackgroundClicked = KvColor.White;
        public KvColor Outline = KvColor.Rgba(0.5529412f, 0.2431373f, 1.0f, 1.0f);
        public KvColor OutlineClicked = KvColor.White;
        public KvColor Text = KvColor.White;
        public KvColor TextClicked = KvColor.Black;

        // 雨线
        public KvColor RainColor = KvColor.Rgba(0.5137255f, 0.1254902f, 0.8588235f, 1.0f);
        public KvColor RainColor2 = KvColor.White;
        public KvColor RainColor3 = KvColor.Rgba(1.0f, 0.0f, 1.0f, 1.0f);

        public KvColor GhostRainColor = KvColor.Rgba(1f, 1f, 1f, 0.6f);
        public KvColor GhostRainColor2 = KvColor.Rgba(1f, 1f, 1f, 0.6f);
        public KvColor GhostRainColor3 = KvColor.Rgba(1f, 1f, 1f, 0.6f);

        // KPS / Total
        public KvColor KpsBackground = KvColor.Rgba(0.5607843f, 0.2352941f, 1.0f, 0.1960784f);
        public KvColor KpsOutline = KvColor.Rgba(0.5529412f, 0.2431373f, 1.0f, 1.0f);
        public KvColor KpsText = KvColor.White;

        public KvColor TotalBackground = KvColor.Rgba(0.5607843f, 0.2352941f, 1.0f, 0.1960784f);
        public KvColor TotalOutline = KvColor.Rgba(0.5529412f, 0.2431373f, 1.0f, 1.0f);
        public KvColor TotalText = KvColor.White;

        // 文本描边 / 阴影
        public bool EnableKeyTextOutline = false;
        public KvColor KeyTextOutlineColor = KvColor.Black;
        public float KeyTextOutlineThickness = 0.2f;

        public bool EnableKeyTextShadow = true;
        public KvColor KeyTextShadowColor = KvColor.Rgba(0f, 0f, 0f, 0.5f);
        public float KeyTextShadowOffsetX = 1f;
        public float KeyTextShadowOffsetY = -1f;

        public bool EnableCountTextOutline = false;
        public KvColor CountTextOutlineColor = KvColor.Black;
        public float CountTextOutlineThickness = 0.2f;

        public bool EnableCountTextShadow = true;
        public KvColor CountTextShadowColor = KvColor.Rgba(0f, 0f, 0f, 0.5f);
        public float CountTextShadowOffsetX = 1f;
        public float CountTextShadowOffsetY = -1f;

        // 键帽圆角与描边宽度（参考画布单位）—— 原版由精灵图 / 程序化网格决定，配置里没有对应项
        public float CornerRadius = KvGeometry.CornerRadius;
        public float OutlineWidth = KvGeometry.OutlineWidth;

        /// <summary>从配置档案生成配色集合 —— 逐字段直搬，不含任何推断。</summary>
        public static KvTheme FromProfile(KvProfile p)
        {
            return new KvTheme
            {
                Background = p.Background,
                BackgroundClicked = p.BackgroundClicked,
                Outline = p.Outline,
                OutlineClicked = p.OutlineClicked,
                Text = p.Text,
                TextClicked = p.TextClicked,

                RainColor = p.RainColor,
                RainColor2 = p.RainColor2,
                RainColor3 = p.RainColor3,
                GhostRainColor = p.GhostRainColor,
                GhostRainColor2 = p.GhostRainColor2,
                GhostRainColor3 = p.GhostRainColor3,

                KpsBackground = p.KpsBackground,
                KpsOutline = p.KpsOutline,
                KpsText = p.KpsText,
                TotalBackground = p.TotalBackground,
                TotalOutline = p.TotalOutline,
                TotalText = p.TotalText,

                EnableKeyTextOutline = p.EnableKeyTextOutline,
                KeyTextOutlineColor = p.KeyTextOutlineColor,
                KeyTextOutlineThickness = p.KeyTextOutlineThickness,
                EnableKeyTextShadow = p.EnableKeyTextShadow,
                KeyTextShadowColor = p.KeyTextShadowColor,
                KeyTextShadowOffsetX = p.KeyTextShadowOffsetX,
                KeyTextShadowOffsetY = p.KeyTextShadowOffsetY,

                EnableCountTextOutline = p.EnableCountTextOutline,
                CountTextOutlineColor = p.CountTextOutlineColor,
                CountTextOutlineThickness = p.CountTextOutlineThickness,
                EnableCountTextShadow = p.EnableCountTextShadow,
                CountTextShadowColor = p.CountTextShadowColor,
                CountTextShadowOffsetX = p.CountTextShadowOffsetX,
                CountTextShadowOffsetY = p.CountTextShadowOffsetY,
            };
        }
    }
}

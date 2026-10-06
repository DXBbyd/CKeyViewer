using System;

namespace CKeyViewer.Core
{
    /// <summary>
    /// 自由布局节点 —— 逐字段对应 jipper 的 <c>JipperKeyViewer.KeyViewer.Settings.FmNode</c>。
    ///
    /// <para><b>NodeType</b>：0 = 普通按键；1 = KPS 统计条；2 = Total 统计条；3 = 图片/视频。</para>
    /// <para>坐标是**画布绝对坐标**（画布高度恒为 1080，宽度 = 屏幕宽高比 × 1080）：
    /// <c>X</c> 为左边缘（自画布左边），<c>Y</c> 为**上边缘距画布顶边**的距离。
    /// 渲染时中心落在 y 向上的 <c>CenterY = 1080 - Y - Height/2</c> 处 —— 与原版一致。</para>
    /// <para>配色数组为 <c>[r,g,b,a]</c>（0~1）；为 null 表示沿用全局配色。</para>
    /// </summary>
    public sealed class KvFmNode
    {
        /// <summary>0=按键 1=KPS 2=Total 3=图片/视频。</summary>
        public int NodeType { get; set; }
        /// <summary>节点唯一 id（配置里的 CustomNodeNextId 递增）。</summary>
        public int Id { get; set; }
        public string KeyBind { get; set; } = "";
        public string GhostKey { get; set; } = "";
        public string CustomText { get; set; } = "";
        public string PressedText { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; } = 60f;
        public float Height { get; set; } = 60f;
        public int Depth { get; set; }
        public int Count { get; set; }
        public bool CountInTotal { get; set; } = true;
        public bool PerKeyKps { get; set; }
        public bool RainEnabled { get; set; }
        public bool Unselectable { get; set; }
        public float Opacity { get; set; } = 1f;
        public string ImagePath { get; set; } = "";
        public string VideoPath { get; set; } = "";
        public bool VideoLoop { get; set; } = true;
        public bool UseCustomColor { get; set; }
        public float[] Bg { get; set; }
        public float[] BgPressed { get; set; }
        public float[] Outline { get; set; }
        public float[] OutlinePressed { get; set; }
        public float[] TextColor { get; set; }
        public float[] TextColorPressed { get; set; }
        public int RainRow { get; set; }
        public float RainWidth { get; set; }
        public float RainHeight { get; set; }
        public float RainSpeed { get; set; }
        public bool UseCustomRainColor { get; set; }
        public float[] RainColorTop { get; set; }
        public float[] RainColorBottom { get; set; }
        public float RainOffsetX { get; set; }
        public float RainOffsetY { get; set; }
        public bool CounterAnimEnabled { get; set; } = true;
        public float CounterAnimScale { get; set; } = 1.1f;
        public bool PressAnimEnabled { get; set; } = true;
        public bool UseCustomPressAnim { get; set; }
        public float PressAnimScale { get; set; } = 0.9f;
        public bool UseCustomPressEasing { get; set; }
        public string PressAnimEasing { get; set; } = "linear";
        public float PressAnimDurationMs { get; set; } = 80f;
        public bool UseCustomStatLayout { get; set; }
        public bool StatCentered { get; set; }
        public bool StatStacked { get; set; }
        public bool UseCustomRainShadow { get; set; }
        public bool RainShadowEnabled { get; set; } = true;
        public float[] RainShadowColor { get; set; }
        public float RainShadowOffsetX { get; set; } = 3f;
        public float RainShadowOffsetY { get; set; } = -3f;
        public bool UseCustomRainOutline { get; set; }
        public bool RainOutlineEnabled { get; set; }
        public float[] RainOutlineColor { get; set; }
        public float RainOutlineWidth { get; set; } = 2f;
        public bool UseCustomGhostRainShadow { get; set; }
        public bool GhostRainShadowEnabled { get; set; } = true;
        public float[] GhostRainShadowColor { get; set; }
        public float GhostRainShadowOffsetX { get; set; } = 3f;
        public float GhostRainShadowOffsetY { get; set; } = -3f;
        public bool UseCustomGhostRainOutline { get; set; }
        public bool GhostRainOutlineEnabled { get; set; }
        public float[] GhostRainOutlineColor { get; set; }
        public float GhostRainOutlineWidth { get; set; } = 2f;
        public bool UseCustomGhostRainParams { get; set; }
        public float GhostRainWidth { get; set; }
        public float GhostRainHeight { get; set; }
        public float GhostRainSpeed { get; set; }
        public float GhostRainOffsetX { get; set; }
        public float GhostRainOffsetY { get; set; }
        public bool UseCustomRainFade { get; set; }
        public bool TrailFadeEnabled { get; set; } = true;
        public float TrailFadePx { get; set; } = 50f;
        public bool ReleaseFadeEnabled { get; set; }
        public float ReleaseFadeDuration { get; set; } = 0.5f;
        public float CounterAnimDurationMs { get; set; } = 300f;
        public float[] CounterAnimBezier { get; set; } = new float[] { 0.25f, 0.46f, 0.45f, 0.94f };
        public string GroupId { get; set; } = "";
        public string ImagePathPressed { get; set; } = "";
        public bool HideLabel { get; set; }
        public bool HideCount { get; set; }
        public bool UseCustomCountFormat { get; set; }
        public bool CountThousandsSeparator { get; set; }
        public float CornerRadius { get; set; }
        public float BorderThickness { get; set; }
        public float FontSize { get; set; }
        public bool Hidden { get; set; }
        public bool UseCustomTextStyle { get; set; }
        public bool KeyTextOutlineEnabled { get; set; }
        public float[] KeyTextOutlineColor { get; set; }
        public float KeyTextOutlineThickness { get; set; } = 0.2f;
        public bool KeyTextShadowEnabled { get; set; } = true;
        public float[] KeyTextShadowColor { get; set; }
        public float KeyTextShadowOffsetX { get; set; } = 1f;
        public float KeyTextShadowOffsetY { get; set; } = -1f;
        public float KeyTextShadowSoftness { get; set; }
        public bool CountTextOutlineEnabled { get; set; }
        public float[] CountTextOutlineColor { get; set; }
        public float CountTextOutlineThickness { get; set; } = 0.2f;
        public bool CountTextShadowEnabled { get; set; } = true;
        public float[] CountTextShadowColor { get; set; }
        public float CountTextShadowOffsetX { get; set; } = 1f;
        public float CountTextShadowOffsetY { get; set; } = -1f;
        public float CountTextShadowSoftness { get; set; }

        // ---------------------------------------------------------------
        // 便捷访问
        // ---------------------------------------------------------------

        /// <summary>节点中心 Y（画布坐标，y 向上为正）。</summary>
        public float CenterY => 1080f - Y - Height * 0.5f;

        /// <summary>左边缘 X。</summary>
        public float Left => X;

        /// <summary>是否统计条（KPS / Total）。</summary>
        public bool IsStat => NodeType == 1 || NodeType == 2;

        /// <summary>是否图片 / 视频节点。</summary>
        public bool IsMedia => NodeType == 3;

        /// <summary>是否是一个真正会显示按键的节点（图片节点绑了键也算）。</summary>
        public bool HasKey => NodeType == 3 ? !string.IsNullOrWhiteSpace(KeyBind) : true;

        /// <summary>节点类型的人话名称。</summary>
        public string TypeName
        {
            get
            {
                switch (NodeType)
                {
                    case 1: return "KPS";
                    case 2: return "Total";
                    case 3: return "图片";
                    default: return "按键";
                }
            }
        }

        /// <summary>深拷贝（复制节点用）。</summary>
        public KvFmNode Clone()
        {
            var n = (KvFmNode)MemberwiseClone();
            n.Bg = CopyArr(Bg);
            n.BgPressed = CopyArr(BgPressed);
            n.Outline = CopyArr(Outline);
            n.OutlinePressed = CopyArr(OutlinePressed);
            n.TextColor = CopyArr(TextColor);
            n.TextColorPressed = CopyArr(TextColorPressed);
            n.RainColorTop = CopyArr(RainColorTop);
            n.RainColorBottom = CopyArr(RainColorBottom);
            n.RainShadowColor = CopyArr(RainShadowColor);
            n.RainOutlineColor = CopyArr(RainOutlineColor);
            n.GhostRainShadowColor = CopyArr(GhostRainShadowColor);
            n.GhostRainOutlineColor = CopyArr(GhostRainOutlineColor);
            n.CounterAnimBezier = CopyArr(CounterAnimBezier);
            n.KeyTextOutlineColor = CopyArr(KeyTextOutlineColor);
            n.KeyTextShadowColor = CopyArr(KeyTextShadowColor);
            n.CountTextOutlineColor = CopyArr(CountTextOutlineColor);
            n.CountTextShadowColor = CopyArr(CountTextShadowColor);
            return n;
        }

        private static float[] CopyArr(float[] a) => a == null ? null : (float[])a.Clone();

        /// <summary>把越界值与 NaN 收拢到合法范围 —— 对齐原版 EnsureCustomNodes 的钳制。</summary>
        public void Sanitize()
        {
            if (NodeType < 0 || NodeType > 3) NodeType = 0;
            X = Safe(X, 0f, -8000f, 8000f);
            Y = Safe(Y, 0f, -8000f, 8000f);
            Width = Safe(Width, 60f, 10f, 2000f);
            Height = Safe(Height, 60f, 10f, 2000f);
            Opacity = float.IsNaN(Opacity) ? 1f : Math.Clamp(Opacity, 0f, 1f);
            RainRow = Math.Clamp(RainRow, 0, 2);
            Depth = Math.Clamp(Depth, -9999, 9999);
            FontSize = (float.IsNaN(FontSize) || FontSize < 0f) ? 0f : Math.Min(FontSize, 72f);
            RainOffsetX = float.IsNaN(RainOffsetX) ? 0f : Math.Clamp(RainOffsetX, -2000f, 2000f);
            RainOffsetY = float.IsNaN(RainOffsetY) ? 0f : Math.Clamp(RainOffsetY, -2000f, 2000f);
            CounterAnimScale = float.IsNaN(CounterAnimScale) ? 1.1f : Math.Clamp(CounterAnimScale, 1f, 2f);
            CounterAnimDurationMs = (CounterAnimDurationMs <= 0f || float.IsNaN(CounterAnimDurationMs)) ? 300f : Math.Min(CounterAnimDurationMs, 5000f);
            if (CounterAnimBezier == null || CounterAnimBezier.Length != 4)
                CounterAnimBezier = new float[] { 0.25f, 0.46f, 0.45f, 0.94f };
            PressAnimScale = float.IsNaN(PressAnimScale) ? 0.9f : Math.Clamp(PressAnimScale, 0.3f, 2f);
            if (string.IsNullOrEmpty(PressAnimEasing)) PressAnimEasing = "linear";
            KeyBind = KeyBind ?? "";
            GhostKey = GhostKey ?? "";
            CustomText = CustomText ?? "";
            PressedText = PressedText ?? "";
            GroupId = GroupId ?? "";
            ImagePath = ImagePath ?? "";
            ImagePathPressed = ImagePathPressed ?? "";
            VideoPath = VideoPath ?? "";
        }

        private static float Safe(float v, float fallback, float lo, float hi)
            => (float.IsNaN(v) || float.IsInfinity(v)) ? fallback : Math.Clamp(v, lo, hi);
    }
}

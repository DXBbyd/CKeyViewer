using System;
using System.Collections.Generic;

namespace CKeyViewer.Core
{
    /// <summary>二维向量，对应 jipper 的 {"x":..,"y":..}。</summary>
    public struct KvPos
    {
        public float x { get; set; }
        public float y { get; set; }

        public KvPos(float x, float y) { this.x = x; this.y = y; }

        public static KvPos Of(float x, float y) => new KvPos(x, y);
    }

    /// <summary>
    /// 与 jipper `config/profiles/&lt;name&gt;.json` **逐字段兼容**的配置档案。
    /// 字段名必须与 JSON 键完全一致（PascalCase），这样原版与本站可互相读取。
    /// </summary>
    public sealed class KvProfile
    {
        // ---- 布局 ----
        public int KeyViewerStyle { get; set; } = 1;        // 1 = Key16
        public int FootKeyViewerStyle { get; set; } = 2;    // 2 = Key4

        public int[] key8 { get; set; } = DefaultKey8();
        public string[] key8Text { get; set; } = DefaultText8();
        public int[] key10 { get; set; } = DefaultKey10();
        public string[] key10Text { get; set; } = DefaultText10();
        public int[] key12 { get; set; } = DefaultKey12();
        public string[] key12Text { get; set; } = DefaultText12();
        public int[] key14 { get; set; } = DefaultKey14();
        public string[] key14Text { get; set; } = DefaultText14();
        public int[] key16 { get; set; } = DefaultKey16();
        public string[] key16Text { get; set; } = DefaultText16();
        public int[] key20 { get; set; } = DefaultKey20();
        public string[] key20Text { get; set; } = DefaultText20();
        public int[] key24 { get; set; } = DefaultKey24();
        public string[] key24Text { get; set; } = DefaultText24();
        public int[] key108 { get; set; } = DefaultKey108();

        public int[] footkey2 { get; set; } = new[] { 289, 284 };
        public int[] footkey4 { get; set; } = new[] { 113, 119, 101, 114 };
        public int[] footkey6 { get; set; } = new[] { 289, 284, 288, 283, 287, 282 };
        public int[] footkey8 { get; set; } = new[] { 289, 285, 288, 284, 287, 283, 286, 282 };
        public int[] footkey10 { get; set; } = new[] { 289, 285, 288, 284, 287, 283, 286, 282, 290, 291 };
        public int[] footkey12 { get; set; } = new[] { 289, 285, 288, 284, 287, 283, 286, 282, 290, 291, 292, 293 };
        public int[] footkey14 { get; set; } = new[] { 289, 285, 288, 284, 287, 283, 286, 282, 290, 291, 292, 293, 294, 295 };
        public int[] footkey16 { get; set; } = new[] { 289, 285, 288, 284, 287, 283, 286, 282, 290, 291, 292, 293, 294, 295, 296, 670 };

        public string[] footkey2Text { get; set; } = Blank(2);
        public string[] footkey4Text { get; set; } = Blank(4);
        public string[] footkey6Text { get; set; } = Blank(6);
        public string[] footkey8Text { get; set; } = Blank(8);
        public string[] footkey10Text { get; set; } = Blank(10);
        public string[] footkey12Text { get; set; } = Blank(12);
        public string[] footkey14Text { get; set; } = Blank(14);
        public string[] footkey16Text { get; set; } = Blank(16);

        // ---- 鬼键（记录哪些键位处于「鬼键」状态）----
        public int[] GhostKey8 { get; set; } = Zeros(8);
        public int[] GhostKey10 { get; set; } = Zeros(10);
        public int[] GhostKey12 { get; set; } = Zeros(12);
        public int[] GhostKey14 { get; set; } = Zeros(14);
        public int[] GhostKey16 { get; set; } = Zeros(16);
        public int[] GhostKey20 { get; set; } = Zeros(20);
        public int[] GhostKey24 { get; set; } = Zeros(24);

        // ---- 统计 ----
        public int[] Count { get; set; } = new int[40];
        public long TotalCount { get; set; }
        public string KpsLabel { get; set; } = "KPS";
        public string TotalLabel { get; set; } = "Total";

        // ---- 基本 ----
        public bool DownLocation { get; set; } = true;
        public float Size { get; set; } = 1.0f;
        public bool Enabled { get; set; } = true;

        // ---- 主键配色 ----
        public KvColor Background { get; set; } = KvColor.Rgba(0.5607843f, 0.2352941f, 1.0f, 0.1960784f);
        public KvColor BackgroundClicked { get; set; } = KvColor.White;
        public KvColor Outline { get; set; } = KvColor.Rgba(0.5529412f, 0.2431373f, 1.0f, 1.0f);
        public KvColor OutlineClicked { get; set; } = KvColor.White;
        public KvColor Text { get; set; } = KvColor.White;
        public KvColor TextClicked { get; set; } = KvColor.Black;

        // ---- 雨线配色 ----
        public KvColor RainColor { get; set; } = KvColor.Rgba(0.5137255f, 0.1254902f, 0.8588235f, 1.0f);
        public KvColor RainColor2 { get; set; } = KvColor.White;
        public KvColor RainColor3 { get; set; } = KvColor.Rgba(1f, 0f, 1f, 1f);

        // ---- KPS / Total 配色 ----
        public KvColor KpsBackground { get; set; } = KvColor.Rgba(0.5607843f, 0.2352941f, 1.0f, 0.1960784f);
        public KvColor KpsOutline { get; set; } = KvColor.Rgba(0.5529412f, 0.2431373f, 1.0f, 1.0f);
        public KvColor KpsText { get; set; } = KvColor.White;
        public KvColor TotalBackground { get; set; } = KvColor.Rgba(0.5607843f, 0.2352941f, 1.0f, 0.1960784f);
        public KvColor TotalOutline { get; set; } = KvColor.Rgba(0.5529412f, 0.2431373f, 1.0f, 1.0f);
        public KvColor TotalText { get; set; } = KvColor.White;

        // ---- 雨线开关与参数 ----
        public bool EnableRainEffect { get; set; } = true;
        public bool EnableRainFade { get; set; } = true;
        public bool EnableGhostRain { get; set; } = true;
        public float RainFadeDuration { get; set; } = 0.5f;
        public bool EnableRainGradient { get; set; }
        public float RainFadePx { get; set; } = 40f;

        public bool EnableRainForRow1 { get; set; } = true;
        public bool EnableRainForRow2 { get; set; } = true;
        public bool EnableRainForRow3 { get; set; } = true;

        public float RainSpeedRow1 { get; set; } = 100f;
        public float RainSpeedRow2 { get; set; } = 100f;
        public float RainSpeedRow3 { get; set; } = 100f;
        public float RainHeightRow1 { get; set; } = 275f;
        public float RainHeightRow2 { get; set; } = 275f;
        public float RainHeightRow3 { get; set; } = 275f;
        public float RainWidthRow1 { get; set; } = 50f;
        public float RainWidthRow2 { get; set; } = 40f;
        public float RainWidthRow3 { get; set; } = 30f;
        public float RainStartYRow1 { get; set; } = -223f;
        public float RainStartYRow2 { get; set; } = -169f;
        public float RainStartYRow3 { get; set; } = -115f;

        public float GhostRainStartYRow1 { get; set; } = -223f;
        public float GhostRainStartYRow2 { get; set; } = -169f;
        public float GhostRainStartYRow3 { get; set; } = -115f;
        public float GhostRainSpeedRow1 { get; set; } = 100f;
        public float GhostRainSpeedRow2 { get; set; } = 100f;
        public float GhostRainSpeedRow3 { get; set; } = 100f;
        public float GhostRainHeightRow1 { get; set; } = 275f;
        public float GhostRainHeightRow2 { get; set; } = 275f;
        public float GhostRainHeightRow3 { get; set; } = 275f;
        public float GhostRainWidthRow1 { get; set; } = 50f;
        public float GhostRainWidthRow2 { get; set; } = 40f;
        public float GhostRainWidthRow3 { get; set; } = 30f;

        // ---- 位置 ----
        public KvPos MainKeyViewerPosition { get; set; } = KvPos.Of(0f, 1f);
        public KvPos FootKeyViewerPosition { get; set; } = KvPos.Of(0.24f, 1f);
        public bool CustomPositionEnabled { get; set; }

        // ---- 字体 ----
        public int FontIndex { get; set; } = 4;
        public string FontName { get; set; } = "MapleStory";
        public int FontStyleFlags { get; set; } = 2;
        public float KeyFontSize { get; set; } = 21f;

        // ---- 显示选项 ----
        public bool EnableCountFormatting { get; set; } = true;
        public bool HideMainKeyCount { get; set; }
        public bool EnablePerKeyKps { get; set; }
        public bool StreamerMode { get; set; }
        public bool StandardKeyWidth { get; set; }
        public bool HideKpsTotalLabel { get; set; }

        // ---- 按压动画 ----
        public bool EnablePressAnimation { get; set; }
        public float PressAnimationScale { get; set; } = 0.8f;
        public bool EnablePressAnimationOnRain { get; set; } = true;
        public string PressAnimationEasing { get; set; } = "linear";
        public float PressAnimationDurationMs { get; set; } = 105.277779f;

        // ---- 键名文本样式 ----
        public bool EnableKeyTextOutline { get; set; }
        public KvColor KeyTextOutlineColor { get; set; } = KvColor.Black;
        public float KeyTextOutlineThickness { get; set; } = 0.2f;
        public bool EnableKeyTextShadow { get; set; } = true;
        public KvColor KeyTextShadowColor { get; set; } = KvColor.Rgba(0f, 0f, 0f, 0.5f);
        public float KeyTextShadowOffsetX { get; set; } = 1f;
        public float KeyTextShadowOffsetY { get; set; } = -1f;
        public float KeyTextShadowSoftness { get; set; }

        // ---- 计数文本样式 ----
        public bool EnableCountTextOutline { get; set; }
        public KvColor CountTextOutlineColor { get; set; } = KvColor.Black;
        public float CountTextOutlineThickness { get; set; } = 0.2f;
        public bool EnableCountTextShadow { get; set; } = true;
        public KvColor CountTextShadowColor { get; set; } = KvColor.Rgba(0f, 0f, 0f, 0.5f);
        public float CountTextShadowOffsetX { get; set; } = 1f;
        public float CountTextShadowOffsetY { get; set; } = -1f;
        public float CountTextShadowSoftness { get; set; }

        // ---- 鬼键雨线配色 ----
        public KvColor GhostRainColor { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.6f);
        public KvColor GhostRainColor2 { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.6f);
        public KvColor GhostRainColor3 { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.6f);

        // ---- 雨线阴影 ----
        public bool EnableRainShadowRow1 { get; set; }
        public bool EnableRainShadowRow2 { get; set; }
        public bool EnableRainShadowRow3 { get; set; }
        public KvColor RainShadowColorRow1 { get; set; } = KvColor.Rgba(0f, 0f, 0f, 0.35f);
        public KvColor RainShadowColorRow2 { get; set; } = KvColor.Rgba(0f, 0f, 0f, 0.35f);
        public KvColor RainShadowColorRow3 { get; set; } = KvColor.Rgba(0f, 0f, 0f, 0.35f);
        public float RainShadowOffsetXRow1 { get; set; } = 3f;
        public float RainShadowOffsetYRow1 { get; set; } = -3f;
        public float RainShadowOffsetXRow2 { get; set; } = 3f;
        public float RainShadowOffsetYRow2 { get; set; } = -3f;
        public float RainShadowOffsetXRow3 { get; set; } = 3f;
        public float RainShadowOffsetYRow3 { get; set; } = -3f;

        // ---- 雨线描边 ----
        public bool EnableRainOutlineRow1 { get; set; }
        public bool EnableRainOutlineRow2 { get; set; }
        public bool EnableRainOutlineRow3 { get; set; }
        public KvColor RainOutlineColorRow1 { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.5f);
        public KvColor RainOutlineColorRow2 { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.5f);
        public KvColor RainOutlineColorRow3 { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.5f);
        public float RainOutlineWidthRow1 { get; set; } = 2f;
        public float RainOutlineWidthRow2 { get; set; } = 2f;
        public float RainOutlineWidthRow3 { get; set; } = 2f;

        // ---- 鬼键雨线阴影 / 描边 ----
        public bool EnableGhostRainShadowRow1 { get; set; }
        public bool EnableGhostRainShadowRow2 { get; set; }
        public bool EnableGhostRainShadowRow3 { get; set; }
        public KvColor GhostRainShadowColorRow1 { get; set; } = KvColor.Rgba(0f, 0f, 0f, 0.35f);
        public KvColor GhostRainShadowColorRow2 { get; set; } = KvColor.Rgba(0f, 0f, 0f, 0.35f);
        public KvColor GhostRainShadowColorRow3 { get; set; } = KvColor.Rgba(0f, 0f, 0f, 0.35f);
        public float GhostRainShadowOffsetXRow1 { get; set; } = 3f;
        public float GhostRainShadowOffsetYRow1 { get; set; } = -3f;
        public float GhostRainShadowOffsetXRow2 { get; set; } = 3f;
        public float GhostRainShadowOffsetYRow2 { get; set; } = -3f;
        public float GhostRainShadowOffsetXRow3 { get; set; } = 3f;
        public float GhostRainShadowOffsetYRow3 { get; set; } = -3f;
        public bool EnableGhostRainOutlineRow1 { get; set; }
        public bool EnableGhostRainOutlineRow2 { get; set; }
        public bool EnableGhostRainOutlineRow3 { get; set; }
        public KvColor GhostRainOutlineColorRow1 { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.5f);
        public KvColor GhostRainOutlineColorRow2 { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.5f);
        public KvColor GhostRainOutlineColorRow3 { get; set; } = KvColor.Rgba(1f, 1f, 1f, 0.5f);
        public float GhostRainOutlineWidthRow1 { get; set; } = 2f;
        public float GhostRainOutlineWidthRow2 { get; set; } = 2f;
        public float GhostRainOutlineWidthRow3 { get; set; } = 2f;

        // ---- 全键盘 / 每键配色 ----
        public bool EnablePerKeyColors { get; set; }
        public bool EnableFullKeyboardUnifiedColor { get; set; } = true;

        public KvColor FullKeyboardBackground { get; set; } = KvColor.Rgba(0.5607843f, 0.2352941f, 1f, 0.1960784f);
        public KvColor FullKeyboardBackgroundClicked { get; set; } = KvColor.White;
        public KvColor FullKeyboardOutline { get; set; } = KvColor.Rgba(0.5529412f, 0.2431373f, 1f, 1f);
        public KvColor FullKeyboardOutlineClicked { get; set; } = KvColor.White;
        public KvColor FullKeyboardText { get; set; } = KvColor.White;
        public KvColor FullKeyboardTextClicked { get; set; } = KvColor.Black;

        public bool FullKeyboardShowKpsTotal { get; set; }
        public KvPos FullKpsPosition { get; set; } = KvPos.Of(0.62f, 0.88f);
        public KvPos FullTotalPosition { get; set; } = KvPos.Of(0.71f, 0.88f);
        public float FullKeyboardKpsTotalSize { get; set; } = 150f;
        public bool KpsTotalCentered { get; set; } = true;
        public bool KpsTotalStackedWhenCentered { get; set; }

        public List<KvColor> PerKeyBackground { get; set; } = Repeat(DefaultKeyBg, PerKeySlots);
        public List<KvColor> PerKeyBackgroundClicked { get; set; } = Repeat(KvColor.White, PerKeySlots);
        public List<KvColor> PerKeyOutline { get; set; } = Repeat(DefaultKeyOutline, PerKeySlots);
        public List<KvColor> PerKeyOutlineClicked { get; set; } = Repeat(KvColor.White, PerKeySlots);

        public List<KvColor> PerKeyText { get; set; } = Repeat(KvColor.White, PerKeySlots);
        public List<KvColor> PerKeyTextClicked { get; set; } = Repeat(KvColor.Black, PerKeySlots);
        public List<KvColor> PerKeyRainColor { get; set; } = Repeat(KvColor.Rgba(1f, 1f, 1f, 0.6f), PerKeySlots);
        public List<KvColor> PerKeyGhostRainColor { get; set; } = Repeat(KvColor.Rgba(1f, 1f, 1f, 0.6f), PerKeySlots);

        /// <summary>每键独立字号。0 表示沿用 <see cref="KeyFontSize"/>。</summary>
        public bool EnablePerKeyTextSize { get; set; }
        public List<float> PerKeyFontSize { get; set; } = RepeatF(0f, PerKeySlots);

        // ---- 自由布局编辑器（原版 Free Layout / FmNode）----
        // 自由布局：节点与图层组。字段与 jipper 的 FmNode / FmLayerGroup 一一对应，
        // 因此配置可以双向读写。KeyViewerStyle == Custom 时才会走这套布局。
        public List<KvFmNode> CustomNodes { get; set; } = new List<KvFmNode>();
        public List<KvFmLayerGroup> LayerGroups { get; set; } = new List<KvFmLayerGroup>();
        public int CustomNodeNextId { get; set; } = 1;
        public int LayerGroupNextId { get; set; } = 1;

        // ===============================================================
        // 默认值常量
        // ===============================================================

        /// <summary>原版每键配色数组的长度（Full108 只有部分键可单独配色）。</summary>
        public const int PerKeySlots = 42;

        public static readonly KvColor DefaultKeyBg = KvColor.Rgba(0.5607843f, 0.2352941f, 1.0f, 0.1960784f);
        public static readonly KvColor DefaultKeyOutline = KvColor.Rgba(0.5529412f, 0.2431373f, 1.0f, 1.0f);

        private static List<KvColor> Repeat(KvColor c, int n)
        {
            var list = new List<KvColor>(n);
            for (int i = 0; i < n; i++) list.Add(c);
            return list;
        }

        private static List<float> RepeatF(float v, int n)
        {
            var list = new List<float>(n);
            for (int i = 0; i < n; i++) list.Add(v);
            return list;
        }

        // ===============================================================
        // 工具
        // ===============================================================

        [System.Text.Json.Serialization.JsonIgnore]
        public KeyviewerStyle StyleEnum => (KeyviewerStyle)KeyViewerStyle;

        [System.Text.Json.Serialization.JsonIgnore]
        public FootKeyviewerStyle FootStyleEnum => (FootKeyviewerStyle)FootKeyViewerStyle;

        /// <summary>根据当前布局样式取对应的键位数组。</summary>
        public int[] KeysFor(KeyviewerStyle style)
        {
            switch (style)
            {
                case KeyviewerStyle.Key8: return key8;
                case KeyviewerStyle.Key10: return key10;
                case KeyviewerStyle.Key12: return key12;
                case KeyviewerStyle.Key14: return key14;
                case KeyviewerStyle.Key16: return key16;
                case KeyviewerStyle.Key20: return key20;
                case KeyviewerStyle.Key24: return key24;
                case KeyviewerStyle.Full108: return key108;
                default: return key16;
            }
        }

        public string[] KeyTextsFor(KeyviewerStyle style)
        {
            switch (style)
            {
                case KeyviewerStyle.Key8: return key8Text;
                case KeyviewerStyle.Key10: return key10Text;
                case KeyviewerStyle.Key12: return key12Text;
                case KeyviewerStyle.Key14: return key14Text;
                case KeyviewerStyle.Key16: return key16Text;
                case KeyviewerStyle.Key20: return key20Text;
                case KeyviewerStyle.Key24: return key24Text;
                default: return key16Text;
            }
        }

        public int[] FootKeysFor(FootKeyviewerStyle style)
        {
            switch (style)
            {
                case FootKeyviewerStyle.Key2: return footkey2;
                case FootKeyviewerStyle.Key4: return footkey4;
                case FootKeyviewerStyle.Key6: return footkey6;
                case FootKeyviewerStyle.Key8: return footkey8;
                case FootKeyviewerStyle.Key10: return footkey10;
                case FootKeyviewerStyle.Key12: return footkey12;
                case FootKeyviewerStyle.Key14: return footkey14;
                case FootKeyviewerStyle.Key16: return footkey16;
                default: return null;
            }
        }

        /// <summary>脚键的键名数组（与 <see cref="FootKeysFor"/> 一一对应）。</summary>
        public string[] FootKeyTextsFor(FootKeyviewerStyle style)
        {
            switch (style)
            {
                case FootKeyviewerStyle.Key2: return footkey2Text;
                case FootKeyviewerStyle.Key4: return footkey4Text;
                case FootKeyviewerStyle.Key6: return footkey6Text;
                case FootKeyviewerStyle.Key8: return footkey8Text;
                case FootKeyviewerStyle.Key10: return footkey10Text;
                case FootKeyviewerStyle.Key12: return footkey12Text;
                case FootKeyviewerStyle.Key14: return footkey14Text;
                case FootKeyviewerStyle.Key16: return footkey16Text;
                default: return null;
            }
        }

        public static int FootKeyCount(FootKeyviewerStyle style)
        {
            switch (style)
            {
                case FootKeyviewerStyle.Key2: return 2;
                case FootKeyviewerStyle.Key4: return 4;
                case FootKeyviewerStyle.Key6: return 6;
                case FootKeyviewerStyle.Key8: return 8;
                case FootKeyviewerStyle.Key10: return 10;
                case FootKeyviewerStyle.Key12: return 12;
                case FootKeyviewerStyle.Key14: return 14;
                case FootKeyviewerStyle.Key16: return 16;
                default: return 0;
            }
        }

        /// <summary>数值越界时回退，避免手工改坏的配置把界面搞崩。</summary>
        public void Sanitize()
        {
            if (KeyViewerStyle < 0 || KeyViewerStyle > 8) KeyViewerStyle = 1;
            if (FootKeyViewerStyle < 0 || FootKeyViewerStyle > 8) FootKeyViewerStyle = 0;
            if (KeyFontSize < 4f || KeyFontSize > 200f) KeyFontSize = 21f;
            if (Size < 0.05f || Size > 10f) Size = 1f;
            if (FontStyleFlags < 0) FontStyleFlags = 0;

            // Count 必须固定 40，与原版 LoadProfile 的校验一致
            if (Count == null) Count = new int[40];
            else if (Count.Length != 40)
            {
                var c = new int[40];
                Array.Copy(Count, c, Math.Min(Count.Length, 40));
                Count = c;
            }

            var style = StyleEnum;
            int need = KvGeometry.TotalKeyCount(style);
            var keys = KeysFor(style);
            if (keys == null || keys.Length < need)
            {
                // 长度不足时用默认键位补齐
                var fallback = DefaultKeysFor(style);
                var fixedArr = new int[need];
                for (int i = 0; i < need; i++)
                    fixedArr[i] = (keys != null && i < keys.Length) ? keys[i] : fallback[i];
                AssignKeys(style, fixedArr);
            }

            KeyTextsFor(style);   // 触发懒补齐
            NormalizeTextArray(style, need);

            var foot = FootKeysFor(FootStyleEnum);
            if (foot == null) FootKeyViewerStyle = 0;
            else NormalizeFootTextArray(FootStyleEnum, foot.Length);

            if (PerKeyBackground == null) PerKeyBackground = Repeat(DefaultKeyBg, PerKeySlots);
            if (PerKeyBackgroundClicked == null) PerKeyBackgroundClicked = Repeat(KvColor.White, PerKeySlots);
            if (PerKeyOutline == null) PerKeyOutline = Repeat(DefaultKeyOutline, PerKeySlots);
            if (PerKeyOutlineClicked == null) PerKeyOutlineClicked = Repeat(KvColor.White, PerKeySlots);
            if (PerKeyText == null) PerKeyText = Repeat(KvColor.White, PerKeySlots);
            if (PerKeyTextClicked == null) PerKeyTextClicked = Repeat(KvColor.Black, PerKeySlots);
            if (PerKeyRainColor == null) PerKeyRainColor = Repeat(KvColor.Rgba(1f, 1f, 1f, 0.6f), PerKeySlots);
            if (PerKeyGhostRainColor == null) PerKeyGhostRainColor = Repeat(KvColor.Rgba(1f, 1f, 1f, 0.6f), PerKeySlots);
            if (PerKeyFontSize == null) PerKeyFontSize = RepeatF(0f, PerKeySlots);

            if (CustomNodes == null) CustomNodes = new List<KvFmNode>();
            if (LayerGroups == null) LayerGroups = new List<KvFmLayerGroup>();
            if (CustomNodeNextId < 1) CustomNodeNextId = 1;
            if (LayerGroupNextId < 1) LayerGroupNextId = 1;

            // 清掉 null 节点、补齐 id、把越界值收拢（对齐原版 EnsureCustomNodes）
            CustomNodes.RemoveAll(n => n == null);
            foreach (var n in CustomNodes)
            {
                if (n.Id <= 0) n.Id = CustomNodeNextId++;
                if (n.Id >= CustomNodeNextId) CustomNodeNextId = n.Id + 1;
                n.Sanitize();
            }
            LayerGroups.RemoveAll(g => g == null);
            foreach (var g in LayerGroups)
            {
                if (string.IsNullOrEmpty(g.Id)) g.Id = "g" + LayerGroupNextId++;
                g.Name = g.Name ?? "";
            }

            if (MainKeyViewerPosition.x == 0f && MainKeyViewerPosition.y == 0f)
                MainKeyViewerPosition = KvPos.Of(0f, 1f);
            if (FootKeyViewerPosition.x == 0f && FootKeyViewerPosition.y == 0f)
                FootKeyViewerPosition = KvPos.Of(0.24f, 1f);
        }

        /// <summary>把键名数组补齐到与键位数组等长（原版的 key*Text 允许为空串）。</summary>
        private void NormalizeTextArray(KeyviewerStyle style, int need)
        {
            var texts = KeyTextsFor(style);
            if (texts == null || texts.Length != need)
            {
                var t = new string[need];
                for (int i = 0; i < need; i++)
                    t[i] = (texts != null && i < texts.Length && texts[i] != null) ? texts[i] : "";
                AssignTexts(style, t);
            }
        }

        private void AssignTexts(KeyviewerStyle style, string[] arr)
        {
            switch (style)
            {
                case KeyviewerStyle.Key8: key8Text = arr; break;
                case KeyviewerStyle.Key10: key10Text = arr; break;
                case KeyviewerStyle.Key12: key12Text = arr; break;
                case KeyviewerStyle.Key14: key14Text = arr; break;
                case KeyviewerStyle.Key16: key16Text = arr; break;
                case KeyviewerStyle.Key20: key20Text = arr; break;
                case KeyviewerStyle.Key24: key24Text = arr; break;
            }
        }

        /// <summary>把脚键键名数组补齐到与脚键键位数组等长。</summary>
        private void NormalizeFootTextArray(FootKeyviewerStyle style, int need)
        {
            var texts = FootKeyTextsFor(style);
            if (texts != null && texts.Length == need) return;

            var t = new string[need];
            for (int i = 0; i < need; i++)
                t[i] = (texts != null && i < texts.Length && texts[i] != null) ? texts[i] : "";

            switch (style)
            {
                case FootKeyviewerStyle.Key2: footkey2Text = t; break;
                case FootKeyviewerStyle.Key4: footkey4Text = t; break;
                case FootKeyviewerStyle.Key6: footkey6Text = t; break;
                case FootKeyviewerStyle.Key8: footkey8Text = t; break;
                case FootKeyviewerStyle.Key10: footkey10Text = t; break;
                case FootKeyviewerStyle.Key12: footkey12Text = t; break;
                case FootKeyviewerStyle.Key14: footkey14Text = t; break;
                case FootKeyviewerStyle.Key16: footkey16Text = t; break;
            }
        }

        private void AssignKeys(KeyviewerStyle style, int[] arr)
        {
            switch (style)
            {
                case KeyviewerStyle.Key8: key8 = arr; break;
                case KeyviewerStyle.Key10: key10 = arr; break;
                case KeyviewerStyle.Key12: key12 = arr; break;
                case KeyviewerStyle.Key14: key14 = arr; break;
                case KeyviewerStyle.Key16: key16 = arr; break;
                case KeyviewerStyle.Key20: key20 = arr; break;
                case KeyviewerStyle.Key24: key24 = arr; break;
                case KeyviewerStyle.Full108: key108 = arr; break;
            }
        }

        public static int[] DefaultKeysFor(KeyviewerStyle style)
        {
            switch (style)
            {
                case KeyviewerStyle.Key8: return DefaultKey8();
                case KeyviewerStyle.Key10: return DefaultKey10();
                case KeyviewerStyle.Key12: return DefaultKey12();
                case KeyviewerStyle.Key14: return DefaultKey14();
                case KeyviewerStyle.Key16: return DefaultKey16();
                case KeyviewerStyle.Key20: return DefaultKey20();
                case KeyviewerStyle.Key24: return DefaultKey24();
                case KeyviewerStyle.Full108: return DefaultKey108();
                default: return DefaultKey16();
            }
        }

        /// <summary>把某一布局样式的键位数组整体替换（设置面板「恢复默认」用）。</summary>
        public void SetKeysFor(KeyviewerStyle style, int[] arr)
        {
            if (arr == null) return;
            AssignKeys(style, arr);
        }

        // ---- 各样式默认键位（取自原版 Default.json）----

        public static int[] DefaultKey8() => new[] { 9, 49, 50, 101, 112, 61, 8, 92 };
        public static int[] DefaultKey10() => new[] { 9, 49, 50, 101, 112, 61, 8, 92, 32, 44 };
        public static int[] DefaultKey12() => new[] { 9, 49, 50, 101, 112, 61, 8, 92, 32, 99, 44, 46 };
        public static int[] DefaultKey14() => new[] { 9, 49, 50, 101, 112, 61, 8, 92, 32, 99, 44, 46, 301, 304 };
        public static int[] DefaultKey16() => new[] { 97, 115, 100, 102, 106, 107, 108, 59, 118, 99, 98, 110, 122, 120, 109, 44 };
        public static int[] DefaultKey20() => new[]
        {
            9, 49, 50, 101, 112, 61, 8, 92, 32, 99, 44, 46, 301, 304, 13, 104, 306, 100, 303, 59
        };
        public static int[] DefaultKey24() => new[]
        {
            9, 49, 50, 101, 112, 61, 8, 92, 32, 99, 44, 46, 301, 304, 13, 104, 306, 100, 303,
            113, 122, 120, 118, 98
        };

        /// <summary>
        /// Full108 的 105 个键位 —— 逐项抄自原版 `config/profiles/Default.json` 的 key108，
        /// 顺序必须与 <see cref="KvGeometry.Full108Table"/> 的槽位下标严格对应。
        /// </summary>
        public static int[] DefaultKey108() => new[]
        {
            // 0..12   Esc + F1..F12
            27, 282, 283, 284, 285, 286, 287, 288, 289, 290, 291, 292, 293,
            // 13..30  PrintScrollLock, ScrollLock, Pause, NumLock, ` 1 2 3 4 5 6 7 8 9 0 - = Backspace
            316, 302, 19, 317, 96, 49, 50, 51, 52, 53, 54, 55, 56, 57, 48, 45, 61, 8,
            // 31..44  Tab Q W E R T Y U I O P [ ] \
            9, 113, 119, 101, 114, 116, 121, 117, 105, 111, 112, 91, 93, 92,
            // 45..57  CapsLock A S D F G H J K L ; ' Enter
            301, 97, 115, 100, 102, 103, 104, 106, 107, 108, 59, 39, 13,
            // 58..69  LShift Z X C V B N M , . / RShift
            304, 122, 120, 99, 118, 98, 110, 109, 44, 46, 47, 303,
            // 70..77  LCtrl LWin LAlt Space RAlt RCtrl LeftCmd Function
            306, 311, 308, 32, 307, 312, 319, 305,
            // 78..83  Insert Del Home End PageUp PageDown
            277, 127, 278, 279, 280, 281,
            // 84..87  Up Left Down Right
            273, 276, 274, 275,
            // 88..91  NumLock / * -
            300, 267, 268, 269,
            // 92..95  numpad 7 8 9 +
            263, 264, 265, 270,
            // 96..98  numpad 4 5 6
            260, 261, 262,
            // 99..101 numpad 1 2 3
            257, 258, 259,
            // 102..104 numpad 0 . Enter
            256, 266, 271
        };

        private static string[] Blank(int n)
        {
            var a = new string[n];
            for (int i = 0; i < n; i++) a[i] = "";
            return a;
        }

        // ===============================================================
        // 自由布局（CustomNodes / LayerGroups）
        // ===============================================================

        /// <summary>节点是否可见：自身没被隐藏，且所属图层组（如果有）是打开状态。</summary>
        public bool NodeVisible(KvFmNode n)
        {
            if (n == null || n.Hidden) return false;
            if (string.IsNullOrEmpty(n.GroupId)) return true;
            var g = GroupFor(n.GroupId);
            return g == null || g.Visible;
        }

        public KvFmLayerGroup GroupFor(string id)
        {
            if (string.IsNullOrEmpty(id) || LayerGroups == null) return null;
            foreach (var g in LayerGroups)
                if (g != null && g.Id == id) return g;
            return null;
        }

        /// <summary>
        /// 参与渲染的节点：可见 + 有键，按 Depth 升序，最多 2048 个
        /// —— 逐条对齐原版 <c>InitializeCustomLayout</c>。
        /// </summary>
        public List<KvFmNode> VisibleNodes()
        {
            var list = new List<KvFmNode>();
            if (CustomNodes == null) return list;
            foreach (var n in CustomNodes)
                if (NodeVisible(n) && n.HasKey) list.Add(n);

            list.Sort((a, b) => a.Depth != b.Depth ? a.Depth.CompareTo(b.Depth) : a.Id.CompareTo(b.Id));
            if (list.Count > 2048) list.RemoveRange(2048, list.Count - 2048);
            return list;
        }

        /// <summary>新建节点，并接管 Id 分配。</summary>
        public KvFmNode NewNode(int nodeType, float x, float y, float width, float height)
        {
            if (CustomNodes == null) CustomNodes = new List<KvFmNode>();
            var n = new KvFmNode
            {
                NodeType = nodeType,
                Id = CustomNodeNextId++,
                X = x,
                Y = y,               // Y 是「上边缘距画布顶边」，与原版 NewNode 的换算一致
                Width = width,
                Height = height
            };
            CustomNodes.Add(n);
            return n;
        }

        public KvFmNode NodeById(int id)
        {
            if (CustomNodes == null) return null;
            foreach (var n in CustomNodes) if (n != null && n.Id == id) return n;
            return null;
        }

        public bool RemoveNode(int id)
        {
            if (CustomNodes == null) return false;
            int i = CustomNodes.FindIndex(n => n != null && n.Id == id);
            if (i < 0) return false;
            CustomNodes.RemoveAt(i);
            return true;
        }

        public KvFmNode DuplicateNode(int id)
        {
            var src = NodeById(id);
            if (src == null) return null;
            var copy = src.Clone();
            copy.Id = CustomNodeNextId++;
            copy.X += 20f;
            copy.Y += 20f;
            CustomNodes.Add(copy);
            return copy;
        }

        /// <summary>Depth 越小的越先画（越靠底层）。</summary>
        public void SetNodeDepth(int id, int depth)
        {
            var n = NodeById(id);
            if (n != null) n.Depth = depth;
        }

        public KvFmLayerGroup NewGroup(string name)
        {
            if (LayerGroups == null) LayerGroups = new List<KvFmLayerGroup>();
            var g = new KvFmLayerGroup
            {
                Id = "g" + LayerGroupNextId++,
                Name = string.IsNullOrEmpty(name) ? ("图层组 " + (LayerGroups.Count + 1)) : name,
                Visible = true
            };
            LayerGroups.Add(g);
            return g;
        }

        public bool RemoveGroup(string id)
        {
            if (LayerGroups == null) return false;
            int i = LayerGroups.FindIndex(g => g != null && g.Id == id);
            if (i < 0) return false;
            string gid = LayerGroups[i].Id;
            LayerGroups.RemoveAt(i);
            // 解绑该组下的节点，它们会回落到「始终可见」
            if (CustomNodes != null)
                foreach (var n in CustomNodes)
                    if (n != null && n.GroupId == gid) n.GroupId = "";
            return true;
        }

        /// <summary>把当前预设布局的键位灌进 CustomNodes（原版「从预设生成自由布局」）。</summary>
        public void RebuildCustomFromPreset(KeyviewerStyle style)
        {
            var slots = KvGeometry.BuildSlots(style, StandardKeyWidth, DownLocation);
            var layout = KvGeometry.GetLayout(style, StandardKeyWidth);
            if (slots.Count == 0) return;

            CustomNodes = new List<KvFmNode>();
            CustomNodeNextId = 1;

            foreach (var s in slots)
            {
                var n = new KvFmNode
                {
                    NodeType = s.Index == -1 ? 1 : s.Index == -2 ? 2 : 0,
                    Id = CustomNodeNextId++,
                    X = s.X,
                    Y = 1080f - s.CenterY - s.H * 0.5f,
                    Width = s.W,
                    Height = s.H,
                    RainEnabled = s.RainRow > 0,
                    RainRow = s.RainRow > 0 ? Math.Clamp(s.RainRow - 1, 0, 2) : 0
                };
                if (s.Index >= 0)
                {
                    var codes = KeysFor(style);
                    if (codes != null && s.Index < codes.Length)
                        n.KeyBind = KeyCodeMap.NameOf(codes[s.Index]);
                }
                CustomNodes.Add(n);
            }
        }

        private static int[] Zeros(int n) => new int[n];

        private static string[] DefaultText8() => Blank(8);
        private static string[] DefaultText10() => Blank(10);
        private static string[] DefaultText12() => Blank(12);
        private static string[] DefaultText14() => Blank(14);
        private static string[] DefaultText16() => Blank(16);
        private static string[] DefaultText20() => Blank(20);
        private static string[] DefaultText24() => Blank(24);
    }
}

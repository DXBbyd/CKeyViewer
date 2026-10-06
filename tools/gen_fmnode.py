"""从反编译出的 FmNode 字段表生成 C# 强类型模型。

用法: python gen_fmnode.py
输出: 打印到 stdout，人工确认后写盘。
"""
import re
import sys

SRC = r"E:/dsh工作区/Keyviever/re/FmNode.cs"
out = []

src = open(SRC, encoding="utf-8", errors="ignore").read()
body = src[src.index("public class FmNode"):]

FIELDS = re.findall(
    r"^\tpublic ([A-Za-z0-9_\[\]<>\.]+) ([A-Za-z0-9_]+)(?:\s*=\s*([^;]+))?;", body, re.M)


def default_for(ctype, raw):
    raw = (raw or "").strip()
    if ctype == "string":
        return ' = "";' if raw == '""' or raw == "" else " = %s;" % raw
    if ctype == "float[]":
        if raw.startswith("new float[4]"):
            # 只取花括号里的内容 —— 否则 float[4] 里的 4 会被当成第一个元素
            brace = re.search(r"\{(.*)\}", raw, re.S)
            nums = re.findall(r"-?\d+(?:\.\d+)?f?", brace.group(1) if brace else "")
            vals = ", ".join(n if n.endswith("f") else n + "f" for n in nums)
            return " = new float[] { %s };" % vals
        return ""          # 其余数组默认 null，表示「未设置，沿用全局」
    if ctype == "float":
        if raw in ("", "0f", "0"):
            return ""
        return " = %s;" % raw
    if ctype == "int":
        return "" if raw in ("", "0") else " = %s;" % raw
    if ctype == "bool":
        return " = true;" if raw == "true" else ""
    return ""


out.append("using System;")
out.append("")
out.append("namespace CKeyViewer.Core")
out.append("{")
out.append("    /// <summary>")
out.append("    /// 自由布局节点 —— 逐字段对应 jipper 的 <c>JipperKeyViewer.KeyViewer.Settings.FmNode</c>。")
out.append("    ///")
out.append("    /// <para><b>NodeType</b>：0 = 普通按键；1 = KPS 统计条；2 = Total 统计条；3 = 图片/视频。</para>")
out.append("    /// <para>坐标是**画布绝对坐标**：<c>X</c> 为左边缘（自画布左边），")
out.append("    /// <c>Y</c> 为**上边缘距画布顶边的距离**。渲染时中心落在 y 向上的")
out.append("    /// <c>CenterY = 1080 - Y - Height/2</c> 处 —— 与原版一致。</para>")
out.append("    /// <para>配色数组为 <c>[r,g,b,a]</c>（0~1）；为 null 表示沿用全局配色。</para>")
out.append("    /// </summary>")
out.append("    public sealed class KvFmNode")
out.append("    {")

last = None
for ctype, name, raw in FIELDS:
    if name in ("NodeType", "Id"):
        out.append("        /// <summary>0=按键 1=KPS 2=Total 3=图片/视频。</summary>" if name == "NodeType"
                   else "        /// <summary>节点唯一 id（配置里的 CustomNodeNextId 递增）。</summary>")
    out.append("        public %s %s { get; set; }%s" % (ctype, name, default_for(ctype, raw)))
    last = name

out.append("")
out.append("        // ---------------------------------------------------------------")
out.append("        // 便捷访问")
out.append("        // ---------------------------------------------------------------")
out.append("")
out.append("        /// <summary>节点中心 Y（画布坐标，y 向上为正）。</summary>")
out.append("        public float CenterY => 1080f - Y - Height * 0.5f;")
out.append("")
out.append("        /// <summary>左边缘 X。</summary>")
out.append("        public float Left => X;")
out.append("")
out.append("        /// <summary>是否统计条（KPS / Total）。</summary>")
out.append("        public bool IsStat => NodeType == 1 || NodeType == 2;")
out.append("")
out.append("        /// <summary>是否图片 / 视频节点。</summary>")
out.append("        public bool IsMedia => NodeType == 3;")
out.append("")
out.append("        /// <summary>是否是一个真正会显示按键的节点（图片节点绑了键也算）。</summary>")
out.append("        public bool HasKey => NodeType == 3 ? !string.IsNullOrWhiteSpace(KeyBind) : true;")
out.append("")
out.append("        /// <summary>节点类型的人话名称。</summary>")
out.append("        public string TypeName")
out.append("        {")
out.append("            get")
out.append("            {")
out.append("                switch (NodeType)")
out.append("                {")
out.append("                    case 1: return \"KPS\";")
out.append("                    case 2: return \"Total\";")
out.append("                    case 3: return \"图片\";")
out.append("                    default: return \"按键\";")
out.append("                }")
out.append("            }")
out.append("        }")
out.append("")
out.append("        /// <summary>深拷贝（复制节点用）。</summary>")
out.append("        public KvFmNode Clone()")
out.append("        {")
out.append("            var n = (KvFmNode)MemberwiseClone();")
out.append("            n.Bg = CopyArr(Bg);")
out.append("            n.BgPressed = CopyArr(BgPressed);")
out.append("            n.Outline = CopyArr(Outline);")
out.append("            n.OutlinePressed = CopyArr(OutlinePressed);")
out.append("            n.TextColor = CopyArr(TextColor);")
out.append("            n.TextColorPressed = CopyArr(TextColorPressed);")
out.append("            n.RainColorTop = CopyArr(RainColorTop);")
out.append("            n.RainColorBottom = CopyArr(RainColorBottom);")
out.append("            n.RainShadowColor = CopyArr(RainShadowColor);")
out.append("            n.RainOutlineColor = CopyArr(RainOutlineColor);")
out.append("            n.GhostRainShadowColor = CopyArr(GhostRainShadowColor);")
out.append("            n.GhostRainOutlineColor = CopyArr(GhostRainOutlineColor);")
out.append("            n.CounterAnimBezier = CopyArr(CounterAnimBezier);")
out.append("            n.KeyTextOutlineColor = CopyArr(KeyTextOutlineColor);")
out.append("            n.KeyTextShadowColor = CopyArr(KeyTextShadowColor);")
out.append("            n.CountTextOutlineColor = CopyArr(CountTextOutlineColor);")
out.append("            n.CountTextShadowColor = CopyArr(CountTextShadowColor);")
out.append("            return n;")
out.append("        }")
out.append("")
out.append("        private static float[] CopyArr(float[] a) => a == null ? null : (float[])a.Clone();")
out.append("")
out.append("        /// <summary>把越界值与 NaN 收拢到合法范围 —— 对齐原版 EnsureCustomNodes 的钳制。</summary>")
out.append("        public void Sanitize()")
out.append("        {")
out.append("            if (NodeType < 0 || NodeType > 3) NodeType = 0;")
out.append("            X = Safe(X, 0f, -8000f, 8000f);")
out.append("            Y = Safe(Y, 0f, -8000f, 8000f);")
out.append("            Width = Safe(Width, 60f, 10f, 2000f);")
out.append("            Height = Safe(Height, 60f, 10f, 2000f);")
out.append("            Opacity = float.IsNaN(Opacity) ? 1f : Math.Clamp(Opacity, 0f, 1f);")
out.append("            RainRow = Math.Clamp(RainRow, 0, 2);")
out.append("            Depth = Math.Clamp(Depth, -9999, 9999);")
out.append("            FontSize = (float.IsNaN(FontSize) || FontSize < 0f) ? 0f : Math.Min(FontSize, 72f);")
out.append("            RainOffsetX = float.IsNaN(RainOffsetX) ? 0f : Math.Clamp(RainOffsetX, -2000f, 2000f);")
out.append("            RainOffsetY = float.IsNaN(RainOffsetY) ? 0f : Math.Clamp(RainOffsetY, -2000f, 2000f);")
out.append("            CounterAnimScale = float.IsNaN(CounterAnimScale) ? 1.1f : Math.Clamp(CounterAnimScale, 1f, 2f);")
out.append("            CounterAnimDurationMs = (CounterAnimDurationMs <= 0f || float.IsNaN(CounterAnimDurationMs)) ? 300f : Math.Min(CounterAnimDurationMs, 5000f);")
out.append("            if (CounterAnimBezier == null || CounterAnimBezier.Length != 4)")
out.append("                CounterAnimBezier = new float[] { 0.25f, 0.46f, 0.45f, 0.94f };")
out.append("            PressAnimScale = float.IsNaN(PressAnimScale) ? 0.9f : Math.Clamp(PressAnimScale, 0.3f, 2f);")
out.append("            if (string.IsNullOrEmpty(PressAnimEasing)) PressAnimEasing = \"linear\";")
out.append("            KeyBind = KeyBind ?? \"\";")
out.append("            GhostKey = GhostKey ?? \"\";")
out.append("            CustomText = CustomText ?? \"\";")
out.append("            PressedText = PressedText ?? \"\";")
out.append("            GroupId = GroupId ?? \"\";")
out.append("            ImagePath = ImagePath ?? \"\";")
out.append("            ImagePathPressed = ImagePathPressed ?? \"\";")
out.append("            VideoPath = VideoPath ?? \"\";")
out.append("        }")
out.append("")
out.append("        private static float Safe(float v, float fallback, float lo, float hi)")
out.append("            => (float.IsNaN(v) || float.IsInfinity(v)) ? fallback : Math.Clamp(v, lo, hi);")
out.append("    }")
out.append("}")

sys.stdout.write("\n".join(out) + "\n")

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CKeyViewer.Core;

namespace CKeyViewer.Render
{
    /// <summary>
    /// 雨线渲染层。参数与算法全部来自对原版 <c>RainSystem</c> / <c>RawRain</c> / <c>RainLayer</c> 的逆向：
    /// <list type="bullet">
    /// <item>宽度取 <c>RainWidthRowN</c>（默认 50/40/30），以键帽中心水平居中。</item>
    /// <item>速度系数 <c>= RainSpeedRowN / 300</c>，乘「经过毫秒」得到行进距离（参考单位）。</item>
    /// <item>轨道高度 <c>= RainHeightRowN</c>（默认 275）。</item>
    /// <item><c>EnableRainGradient</c> 打开时，条顶最后 <c>RainFadePx</c> 像素渐隐（对应原版
    /// <c>RainLayer.DrawRainQuad</c> 的 <c>AlphaAtD</c>）。</item>
    /// <item>可选的阴影 / 描边由各行的 <c>EnableRainShadowRowN</c> / <c>EnableRainOutlineRowN</c> 控制。</item>
    /// </list>
    /// </summary>
    public sealed class KvRainLayer : IRainLayer
    {
        /// <summary>原版常量：雨线轨道高度基准。</summary>
        public const double ContainerHeight = 275.0;

        /// <summary>速度换算常量 —— 原版是 <c>RainSpeed / 300</c> 得到「单位/毫秒」。</summary>
        private const double SpeedDivisor = 300.0;

        private readonly KvProfileStore _store;
        private readonly Dictionary<int, KeySlot> _slotByIndex = new Dictionary<int, KeySlot>();
        private readonly List<KeySlot> _slotSource = new List<KeySlot>();

        public KvRainLayer(KvProfileStore store)
        {
            _store = store;
        }

        private KvProfile P => _store.Profile;

        /// <summary>活跃雨线总数（诊断用）。</summary>
        public int ActiveCount { get; private set; }

        // ===============================================================
        // 参数取用（分行）
        // ===============================================================

        private double Speed(int row, bool ghost)
        {
            switch (row)
            {
                case 1: return ghost ? P.GhostRainSpeedRow2 : P.RainSpeedRow2;
                case 2: return ghost ? P.GhostRainSpeedRow3 : P.RainSpeedRow3;
                default: return ghost ? P.GhostRainSpeedRow1 : P.RainSpeedRow1;
            }
        }

        private double TrackHeight(int row, bool ghost)
        {
            switch (row)
            {
                case 1: return ghost ? P.GhostRainHeightRow2 : P.RainHeightRow2;
                case 2: return ghost ? P.GhostRainHeightRow3 : P.RainHeightRow3;
                default: return ghost ? P.GhostRainHeightRow1 : P.RainHeightRow1;
            }
        }

        /// <summary>基线相对键底边的偏移 = RainStartY + ContainerHeight（原版 num7 + 275）。</summary>
        private double BaseOffset(int row, bool ghost)
        {
            double start, ghostStart;
            switch (row)
            {
                case 1: start = P.RainStartYRow2; ghostStart = P.GhostRainStartYRow2; break;
                case 2: start = P.RainStartYRow3; ghostStart = P.GhostRainStartYRow3; break;
                default: start = P.RainStartYRow1; ghostStart = P.GhostRainStartYRow1; break;
            }
            // 原版：ghost 的 startY = GhostRainStartY - RainStartY（相对增量）
            double delta = ghost ? (ghostStart - start) : 0.0;
            return start + ContainerHeight + delta;
        }

        private double Width(int row, bool ghost)
        {
            switch (row)
            {
                case 1: return ghost ? P.GhostRainWidthRow2 : P.RainWidthRow2;
                case 2: return ghost ? P.GhostRainWidthRow3 : P.RainWidthRow3;
                default: return ghost ? P.GhostRainWidthRow1 : P.RainWidthRow1;
            }
        }

        private KvColor MainColor(int row, bool ghost)
        {
            if (ghost)
            {
                switch (row)
                {
                    case 1: return P.GhostRainColor2;
                    case 2: return P.GhostRainColor3;
                    default: return P.GhostRainColor;
                }
            }
            switch (row)
            {
                case 1: return P.RainColor2;
                case 2: return P.RainColor3;
                default: return P.RainColor;
            }
        }

        private bool RowEnabled(int row)
        {
            switch (row)
            {
                case 2: return P.EnableRainForRow3;
                case 1: return P.EnableRainForRow2;
                default: return P.EnableRainForRow1;
            }
        }

        // ===============================================================
        // 触发 / 释放
        // ===============================================================

        /// <summary>原版行号规则：下标 &lt;8 → 第 1 行，8..15 → 第 2 行，其余 → 第 3 行。</summary>
        public static int RowOf(int slotIndex)
        {
            if (slotIndex < 8) return 0;
            if (slotIndex < 16) return 1;
            return 2;
        }

        /// <summary>
        /// 求该键生效的行号（0 基）。<c>RainRow1</c> 为 -1 时沿用按下标推导的规则（预设布局）；
        /// 为 0 表示该键不画雨线；1..3 是自由布局节点指定的行。
        /// </summary>
        private static int RowIndexOf(KeyRuntime key)
        {
            if (key.RainRow1 < 0) return RowOf(key.SlotIndex);
            return key.RainRow1 - 1;
        }

        public void Trigger(KeyRuntime key, double now)
        {
            if (!P.EnableRainEffect) return;
            if (key.IsFootKey) return;                            // 脚键不画雨线（同原版 raining = -1）
            if (KvGeometry.IsFullKeyboard(P.StyleEnum)) return;   // 全键盘不画雨线（同原版）
            if (key.RainRow1 == 0) return;                        // 节点关掉了雨线

            int row = RowIndexOf(key);
            if (row < 0) return;
            if (!RowEnabled(row)) return;

            key.Rain.Drops.Add(RainDrop.Spawn(key.SlotIndex, row, ghost: false, now));
        }
        public void Release(KeyRuntime key, double now)
        {
            var drops = key.Rain.Drops;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                if (drops[i].Ghost) continue;
                drops[i].Growing = false;
                if (drops[i].FrozenTravel < 0) drops[i].FrozenTravel = TravelOf(drops[i], now);
                if (P.EnableRainFade)
                {
                    drops[i].FadeStart = now;
                }
                break;
            }
        }

        public void TriggerGhost(KeyRuntime key, double now)
        {
            if (!P.EnableRainEffect || !P.EnableGhostRain) return;
            if (key.IsFootKey) return;
            if (KvGeometry.IsFullKeyboard(P.StyleEnum)) return;
            if (key.RainRow1 == 0) return;                        // 节点关掉了雨线

            int row = RowIndexOf(key);
            if (row < 0) return;
            if (!RowEnabled(row)) return;

            key.Rain.Drops.Add(RainDrop.Spawn(key.SlotIndex, row, ghost: true, now));
        }

        public void ReleaseGhost(KeyRuntime key, double now)
        {
            var drops = key.Rain.Drops;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                if (!drops[i].Ghost) continue;
                drops[i].Growing = false;
                if (drops[i].FrozenTravel < 0) drops[i].FrozenTravel = TravelOf(drops[i], now);
                break;
            }
        }

        public void ClearAll(IEnumerable<KeyRuntime> keys)
        {
            foreach (var k in keys) k.Rain.Clear();
            ActiveCount = 0;
        }

        /// <summary>求某条雨线在 now 时刻已经行进的距离（参考单位）。</summary>
        private double TravelOf(RainDrop d, double now)
        {
            double speed = Math.Max(Speed(d.Row, d.Ghost) / SpeedDivisor, 0.0001);
            return Math.Max(0, now - d.Birth) * 1000.0 * speed;
        }

        // ===============================================================
        // 每帧推进
        // ===============================================================

        /// <summary>推进所有雨线并回收已结束的。返回是否仍有活跃雨线（用于决定重绘）。</summary>
        public bool Update(IEnumerable<KeyRuntime> keys, double now)
        {
            int alive = 0;

            foreach (var key in keys)
            {
                var drops = key.Rain.Drops;
                if (drops.Count == 0) continue;

                for (int i = drops.Count - 1; i >= 0; i--)
                {
                    if (!Advance(drops[i], now)) drops.RemoveAt(i);
                }
                alive += drops.Count;
            }

            ActiveCount = alive;
            return alive > 0;
        }
        private bool Advance(RainDrop d, double now)
        {
            double speed = Math.Max(Speed(d.Row, d.Ghost) / SpeedDivisor, 0.0001);   // 参考单位/毫秒
            double track = Math.Max(TrackHeight(d.Row, d.Ghost), 1.0);

            double travelMs = Math.Max(0, now - d.Birth) * 1000.0;
            double traveled = travelMs * speed;

            // 可见区间（距基线的距离）
            double topD = Math.Min(traveled, track);
            double barH;

            if (d.Growing)
            {
                barH = topD;                       // 生长中的高度就是已行进距离
            }
            else
            {
                if (d.FrozenTravel < 0) d.FrozenTravel = traveled;
                barH = Math.Min(d.FrozenTravel, track);
            }

            double bottomD = topD - barH;

            d.BottomD = bottomD;
            d.TopD = topD;
            d.DFar = topD;
            d.DNear = bottomD;
            d.TrackHeight = track;

            // 淡出：原版 alpha = 1 - t*(2-t)
            d.Alpha = 1.0;
            if (!double.IsNaN(d.FadeStart))
            {
                double fade = Math.Max(0.01, P.RainFadeDuration);
                double t = Math.Clamp((now - d.FadeStart) / fade, 0.0, 1.0);
                d.Alpha = 1.0 - t * (2.0 - t);
                if (t >= 1.0) { d.Dead = true; return false; }
            }

            // 兜底回收：原版在「已行进 > 条身高度 + 轨道高」时销毁
            if (!d.Growing && traveled > barH + track + 1.0)
            {
                d.Dead = true;
                return false;
            }

            // 可见高度为零时才回收。注意：生成的那一帧长度必然是 0，
            // 只要仍在生长就必须保留，否则雨线会在出生的瞬间被误杀。
            if (topD - bottomD > 0.05) return true;
            return d.Growing;
        }

        // ===============================================================
        // 绘制
        // ===============================================================

        public void Draw(DrawingContext dc, OverlayRenderer host, double ppd)
        {
            if (host.Keys == null || host.Slots == null) return;
            if (host.Scale <= 0) return;

            SyncSlots(host.Slots);

            foreach (var kv in host.Keys)
            {
                var key = kv.Value;
                var drops = key.Rain.Drops;
                if (drops.Count == 0) continue;

                if (!_slotByIndex.TryGetValue(key.SlotIndex, out var slot)) continue;

                for (int i = 0; i < drops.Count; i++)
                {
                    // 原版先画普通雨线、再画鬼键雨线（后者在上）
                    var d = drops[i];
                    if (d.Dead || d.Alpha <= 0.002) continue;
                    double anim = (host.EnablePressAnimation && host.AnimAffectsRain) ? key.AnimScale : 1.0;
                    if (anim < 0.05 || anim > 4.0) anim = 1.0;
                    DrawDrop(dc, host, slot, d, anim);
                }
            }
        }

        private void SyncSlots(IList<KeySlot> slots)
        {
            if (ReferenceEquals(_slotSource, slots) && _slotSource.Count == slots.Count) return;
            _slotSource.Clear();
            _slotByIndex.Clear();
            foreach (var s in slots)
            {
                if (s.Index < 0) continue;
                if (!_slotByIndex.ContainsKey(s.Index)) _slotByIndex[s.Index] = s;
            }
        }

        private void DrawDrop(DrawingContext dc, OverlayRenderer host, KeySlot slot, RainDrop d, double anim)
        {
            double scale = host.Scale;
            double w = Math.Max(1.0, Width(d.Row, d.Ghost)) * scale * anim;

            // 基线（参考坐标系，y 向上、原点为区块底边）。
            // 按压动画以键帽中心为原点缩放，会把键底边一起抬起来，基线随之移动。
            double bottomEdge = slot.CenterY - (slot.CenterY - slot.Bottom) * anim;
            double baseY = bottomEdge + BaseOffset(d.Row, d.Ghost);

            double bottomRef = baseY + d.BottomD;
            double topRef = baseY + d.TopD;

            double cx = (slot.X + slot.W * 0.5 - host.OriginX) * scale;

            double yTop = (host.TopExtent - topRef) * scale;
            double yBottom = (host.TopExtent - bottomRef) * scale;
            double h = yBottom - yTop;
            if (w <= 0.5 || h <= 0.5) return;

            var rect = new Rect(cx - w * 0.5, yTop, w, h);

            var color = MainColor(d.Row, d.Ghost);

            // ---- 阴影 ----
            if (ShadowEnabled(d.Row, d.Ghost))
            {
                var sc = ShadowColor(d.Row, d.Ghost);
                double dx = ShadowOffsetX(d.Row, d.Ghost) * scale;
                double dy = ShadowOffsetY(d.Row, d.Ghost) * scale;
                var sr = new Rect(rect.X + dx, rect.Y + dy, w, h);
                FillDrop(dc, host, sr, sr, sc, d, color, color);
            }

            // ---- 描边 ----
            if (OutlineEnabled(d.Row, d.Ghost))
            {
                var oc = OutlineColor(d.Row, d.Ghost);
                double ow = Math.Max(0.5, OutlineWidth(d.Row, d.Ghost) * scale);
                var orr = new Rect(rect.X - ow, rect.Y - ow, w + ow * 2, h + ow * 2);
                // 原版描边只是一圈更宽的实心矩形（键帽压在中间遮住内部）
                FillDrop(dc, host, orr, rect, oc, d, color, color);
            }

            // ---- 主体 ----
            FillDrop(dc, host, rect, rect, color, d, color, color);
        }

        /// <summary>
        /// 填充一条雨线。<paramref name="brushBox"/> 是刷子的坐标范围（阴影/描边比本体大），
        /// <paramref name="barBox"/> 是本体的实际范围 —— 渐变按本体的 d 区间换算到刷子坐标系。
        /// </summary>
        private void FillDrop(DrawingContext dc, OverlayRenderer host, Rect brushBox, Rect barBox,
                             KvColor color, RainDrop d, KvColor colBot, KvColor colTop)
        {
            double alpha = d.Alpha * color.a;
            if (alpha <= 0.002) return;

            bool simple = !P.EnableRainGradient
                          || P.RainFadePx <= 0.5
                          || d.TrackHeight <= 0.5
                          || (d.DFar - d.DNear) <= 0.0001;

            if (simple)
            {
                dc.DrawRectangle(color.WithAlpha((float)alpha).ToBrush(), null, brushBox);
                return;
            }

            // 原版 AlphaAtD(d) = 1（d <= track - fade）；(track - d)/fade（中间）；0（d >= track）
            double track = d.TrackHeight;
            double fade = Math.Max(0.5, P.RainFadePx);
            double fadeStartD = track - fade;

            double span = d.DFar - d.DNear;
            if (span <= 0.0001)
            {
                dc.DrawRectangle(color.WithAlpha((float)alpha).ToBrush(), null, brushBox);
                return;
            }

            double aNear = AlphaAtD(d.DNear, fadeStartD, track, fade);
            double aFar = AlphaAtD(d.DFar, fadeStartD, track, fade);

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),   // 底
                EndPoint = new Point(0, 0),     // 顶
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };

            double pFade = (fadeStartD - d.DNear) / span;

            brush.GradientStops.Add(new GradientStop(
                colBot.WithAlpha((float)(alpha * aNear)).ToMediaColor(), 0.0));

            if (pFade > 0.0 && pFade < 1.0)
            {
                brush.GradientStops.Add(new GradientStop(
                    KvColor.Lerp(colBot, colTop, (float)pFade).WithAlpha((float)alpha).ToMediaColor(), pFade));
            }
            else if (pFade >= 1.0)
            {
                // 整条都还没进入渐隐区 → 上端仍是全不透明
                brush.GradientStops.Add(new GradientStop(
                    colTop.WithAlpha((float)alpha).ToMediaColor(), 1.0));
                brush.Freeze();
                dc.DrawRectangle(brush, null, brushBox);
                return;
            }

            brush.GradientStops.Add(new GradientStop(
                colTop.WithAlpha((float)(alpha * aFar)).ToMediaColor(), 1.0));

            brush.Freeze();
            dc.DrawRectangle(brush, null, brushBox);
        }

        private static double AlphaAtD(double d, double fadeStartD, double trackH, double fade)
        {
            if (d <= fadeStartD) return 1.0;
            if (d >= trackH) return 0.0;
            return (trackH - d) / fade;
        }

        // ---- 行参数取用 ----

        private bool ShadowEnabled(int row, bool ghost)
        {
            if (ghost)
            {
                switch (row)
                {
                    case 1: return P.EnableGhostRainShadowRow2;
                    case 2: return P.EnableGhostRainShadowRow3;
                    default: return P.EnableGhostRainShadowRow1;
                }
            }
            switch (row)
            {
                case 1: return P.EnableRainShadowRow2;
                case 2: return P.EnableRainShadowRow3;
                default: return P.EnableRainShadowRow1;
            }
        }

        private KvColor ShadowColor(int row, bool ghost)
        {
            if (ghost)
            {
                switch (row)
                {
                    case 1: return P.GhostRainShadowColorRow2;
                    case 2: return P.GhostRainShadowColorRow3;
                    default: return P.GhostRainShadowColorRow1;
                }
            }
            switch (row)
            {
                case 1: return P.RainShadowColorRow2;
                case 2: return P.RainShadowColorRow3;
                default: return P.RainShadowColorRow1;
            }
        }

        private double ShadowOffsetX(int row, bool ghost)
        {
            if (ghost)
            {
                switch (row)
                {
                    case 1: return P.GhostRainShadowOffsetXRow2;
                    case 2: return P.GhostRainShadowOffsetXRow3;
                    default: return P.GhostRainShadowOffsetXRow1;
                }
            }
            switch (row)
            {
                case 1: return P.RainShadowOffsetXRow2;
                case 2: return P.RainShadowOffsetXRow3;
                default: return P.RainShadowOffsetXRow1;
            }
        }

        private double ShadowOffsetY(int row, bool ghost)
        {
            if (ghost)
            {
                switch (row)
                {
                    case 1: return P.GhostRainShadowOffsetYRow2;
                    case 2: return P.GhostRainShadowOffsetYRow3;
                    default: return P.GhostRainShadowOffsetYRow1;
                }
            }
            switch (row)
            {
                case 1: return P.RainShadowOffsetYRow2;
                case 2: return P.RainShadowOffsetYRow3;
                default: return P.RainShadowOffsetYRow1;
            }
        }

        private bool OutlineEnabled(int row, bool ghost)
        {
            if (ghost)
            {
                switch (row)
                {
                    case 1: return P.EnableGhostRainOutlineRow2;
                    case 2: return P.EnableGhostRainOutlineRow3;
                    default: return P.EnableGhostRainOutlineRow1;
                }
            }
            switch (row)
            {
                case 1: return P.EnableRainOutlineRow2;
                case 2: return P.EnableRainOutlineRow3;
                default: return P.EnableRainOutlineRow1;
            }
        }

        private KvColor OutlineColor(int row, bool ghost)
        {
            if (ghost)
            {
                switch (row)
                {
                    case 1: return P.GhostRainOutlineColorRow2;
                    case 2: return P.GhostRainOutlineColorRow3;
                    default: return P.GhostRainOutlineColorRow1;
                }
            }
            switch (row)
            {
                case 1: return P.RainOutlineColorRow2;
                case 2: return P.RainOutlineColorRow3;
                default: return P.RainOutlineColorRow1;
            }
        }

        private double OutlineWidth(int row, bool ghost)
        {
            if (ghost)
            {
                switch (row)
                {
                    case 1: return P.GhostRainOutlineWidthRow2;
                    case 2: return P.GhostRainOutlineWidthRow3;
                    default: return P.GhostRainOutlineWidthRow1;
                }
            }
            switch (row)
            {
                case 1: return P.RainOutlineWidthRow2;
                case 2: return P.RainOutlineWidthRow3;
                default: return P.RainOutlineWidthRow1;
            }
        }
    }
}

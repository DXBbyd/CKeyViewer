using System.Collections.Generic;

namespace CKeyViewer.Core
{
    /// <summary>
    /// 一条雨线。**语义完全还原自原版 RainSystem.RawRain**：
    /// <list type="bullet">
    /// <item>按下时在「基线」上生成，基线位于键帽上方（<c>键底边 + RainStartY + 275</c>）。</item>
    /// <item>按住期间它从基线**向上生长**，高度 = 已行进距离 = <c>经过毫秒 × RainSpeed / 300</c>。</item>
    /// <item>松开后停止生长、脱离基线，整条继续上升并淡出（就是 KV 招牌的「雨」）。</item>
    /// </list>
    /// 「距离」都以参考单位计（画布高 1080）。
    /// </summary>
    public sealed class RainDrop
    {
        /// <summary>所属键槽下标（对应 KvProfile 的键位数组下标，决定第几行雨线）。</summary>
        public int SlotIndex;

        /// <summary>0/1/2 —— 对应 Row1/Row2/Row3 参数组。</summary>
        public int Row;

        public bool Ghost;

        /// <summary>生成时刻（秒）。</summary>
        public double Birth;

        /// <summary>是否仍在生长（按住中）。</summary>
        public bool Growing = true;

        /// <summary>停止生长那一刻的已行进距离；-1 表示尚未停止。</summary>
        public double FrozenTravel = -1;

        /// <summary>淡出起点时刻；NaN 表示不淡出。</summary>
        public double FadeStart = double.NaN;

        /// <summary>本帧计算出的可见区间（距基线的距离，向上为正）。</summary>
        public double BottomD;
        public double TopD;

        /// <summary>t = dFar / trackHeight，用于顶部渐隐。</summary>
        public double DFar;
        public double DNear;
        public double TrackHeight;

        /// <summary>当前总不透明度（含淡出）。</summary>
        public double Alpha = 1.0;

        /// <summary>标记为可回收。</summary>
        public bool Dead;

        public static RainDrop Spawn(int slotIndex, int row, bool ghost, double now)
        {
            return new RainDrop
            {
                SlotIndex = slotIndex,
                Row = row,
                Ghost = ghost,
                Birth = now
            };
        }
    }

    /// <summary>一个键上所有活跃雨线的容器（对应原版 Key.rainList）。</summary>
    public sealed class RainBucket
    {
        public readonly List<RainDrop> Drops = new List<RainDrop>(8);

        public void Clear() => Drops.Clear();
    }
}

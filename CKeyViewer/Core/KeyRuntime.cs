using System;
using System.Collections.Generic;

namespace CKeyViewer.Core
{
    /// <summary>单个按键的运行时状态。</summary>
    public sealed class KeyRuntime
    {
        /// <summary>Unity KeyCode（与配置一致）。</summary>
        public int UnityKeyCode;

        /// <summary>布局槽位下标 —— 同时是配置里 Count[] 的下标，也决定雨线行号。</summary>
        public int SlotIndex;

        /// <summary>Win32 虚拟键码。</summary>
        public int Vk;

        /// <summary>鬼键的 Unity KeyCode（0 = 未绑定）。按下它会触发「鬼键雨线」，但不计数。</summary>
        public int GhostUnityKeyCode;

        /// <summary>鬼键的 Win32 虚拟键码。</summary>
        public int GhostVk;

        /// <summary>键帽显示名（可由配置覆盖）。</summary>
        public string Label;

        /// <summary>按下时替换显示的文本（自由布局节点的 PressedText）。空串表示不替换。</summary>
        public string PressedLabel;

        /// <summary>
        /// 该键所属的雨线行（**1 基**）：0 = 不画雨线，-1 = 由槽位下标推导。
        /// 预设布局一律用 -1（沿用原版「下标 &lt;8 → 行1，8..15 → 行2，其余 → 行3」）；
        /// 自由布局的节点由 <c>RainEnabled</c> / <c>RainRow</c> 直接指定。
        /// </summary>
        public int RainRow1 = -1;

        /// <summary>累计按下次数。</summary>
        public long Count;

        /// <summary>本帧是否按下。</summary>
        public bool Pressed;

        /// <summary>上一帧是否按下（用于边沿检测）。</summary>
        public bool WasPressed;

        /// <summary>鬼键本帧是否按下。</summary>
        public bool GhostPressed;

        /// <summary>鬼键上一帧是否按下。</summary>
        public bool GhostWasPressed;

        /// <summary>最近一次按下的时间戳（秒，用于 KPS 与雨线）。</summary>
        public double LastPressTime;

        /// <summary>按下时的时间戳，用于按压动画进度。</summary>
        public double PressAnimStart = double.MinValue;

        // ---- 按压动画（对应原版 AnimateKeyScale）----
        // 原版：localScale 从当前值按缓动插值到目标（按下 = PressAnimationScale，松开 = 1），
        // 时长为 PressAnimationDurationMs。这里用「起点 + 起算时间」的等价形式，
        // 每帧按缓动曲线求值，避免协程。

        /// <summary>动画起点的缩放值。</summary>
        public double AnimFrom = 1.0;

        /// <summary>动画目标缩放值。</summary>
        public double AnimTo = 1.0;

        /// <summary>动画起算时间（秒）。</summary>
        public double AnimStart = double.MinValue;

        /// <summary>当前帧的缩放值（1 = 原始大小）。</summary>
        public double AnimScale = 1.0;

        /// <summary>该键是否属于脚键（脚键不显示计数、不画雨线）。</summary>
        public bool IsFootKey;

        /// <summary>
        /// 按下时是否累加 Total 总数。自由布局节点由 <c>FmNode.CountInTotal</c> 决定，
        /// 预设布局一律累加。
        /// </summary>
        public bool CountInTotal = true;

        /// <summary>启动一次按压缩放动画。</summary>
        public void StartAnim(double target, double now)
        {
            AnimFrom = AnimScale;
            AnimTo = target;
            AnimStart = now;
        }

        /// <summary>按缓动推进动画。<paramref name="durationSec"/> &lt;= 0 时直接跳到目标。</summary>
        public void AdvanceAnim(double now, double durationSec, string easing)
        {
            if (AnimStart == double.MinValue || durationSec <= 0.0)
            {
                AnimScale = AnimTo;
                return;
            }
            double t = (now - AnimStart) / durationSec;
            if (t >= 1.0) { AnimScale = AnimTo; return; }
            if (t <= 0.0) { AnimScale = AnimFrom; return; }
            AnimScale = AnimFrom + (AnimTo - AnimFrom) * KvEasing.Ease(easing, t);
        }

        public readonly Queue<double> KpsLog = new Queue<double>(64);

        public int Kps;

        /// <summary>该键上所有活跃雨线（对应原版 Key.rainList）。</summary>
        public readonly RainBucket Rain = new RainBucket();

        public string DisplayLabel
        {
            get
            {
                if (Pressed && !string.IsNullOrEmpty(PressedLabel)) return PressedLabel;
                if (!string.IsNullOrEmpty(Label)) return Label;
                return KeyCodeMap.DisplayName(UnityKeyCode);
            }
        }

        /// <summary>记录一次按下并维护 KPS 滑动窗口（1 秒）。</summary>
        public void RecordPress(double now)
        {
            Count++;
            LastPressTime = now;
            PressAnimStart = now;
            KpsLog.Enqueue(now);
            TrimKpsLog(now);
            Kps = KpsLog.Count;
        }

        public void TrimKpsLog(double now)
        {
            while (KpsLog.Count > 0 && now - KpsLog.Peek() > 1.0) KpsLog.Dequeue();
            Kps = KpsLog.Count;
        }

        public void ResetCounts()
        {
            Count = 0;
            KpsLog.Clear();
            Kps = 0;
        }
    }

    /// <summary>计数格式化 —— 与原版 NumBuffer 行为一致（支持千分位）。</summary>
    public static class NumFormat
    {
        public static string Format(long value, bool thousands)
        {
            return thousands ? value.ToString("N0") : value.ToString();
        }
    }
}

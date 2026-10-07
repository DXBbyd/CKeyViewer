using System.Windows;

namespace CKeyViewer.Core
{
    /// <summary>
    /// 覆盖层的吸附锚点。<c>0</c> = 自由（按各页面里手设的位置摆放），
    /// <c>1..9</c> 是工作区九宫格（含正中）。
    /// <para>
    /// 之所以是 0..9 而不是枚举名列表：配置文件里存的就是这个整数，
    /// 顺序即九宫格从左到右、从上到下，算行列时直接取模取商即可。
    /// </para>
    /// </summary>
    public enum KvAnchor
    {
        Free = 0,
        TopLeft = 1, TopCenter = 2, TopRight = 3,
        MiddleLeft = 4, Center = 5, MiddleRight = 6,
        BottomLeft = 7, BottomCenter = 8, BottomRight = 9,
    }

    /// <summary>
    /// 「吸附」的通用算法：把一块 <c>w×h</c> 的矩形摆到工作区的某个锚点上。
    /// <para>
    /// 三条设计约束（用户明确要求）：
    /// 只改位置、<b>绝不改大小</b>；对着<b>工作区</b>吸附（自动避开任务栏）；
    /// 每次调用都重新读一遍工作区 ⇒ 分辨率 / 任务栏 / 多屏布局一变，
    /// 下一次调用就自动吸到新位置，这就是「动态吸附」。
    /// </para>
    /// </summary>
    public static class KvSnap
    {
        public const int Min = 0;
        public const int Max = 9;

        /// <summary>九宫格 + 自由，下标即配置里存的整数。</summary>
        public static readonly string[] Names =
        {
            "自由", "左上", "中上", "右上",
            "左中", "正中", "右中",
            "左下", "中下", "右下",
        };

        public static string NameOf(int anchor) => Names[Clamp(anchor)];

        public static int Clamp(int v) => v < Min ? Min : (v > Max ? Max : v);

        /// <summary>
        /// 按键层**当前真正生效**的锚点。
        /// <para>
        /// 「吸附到游戏窗口」开着时，按键贴的是游戏窗口、锚点取游戏那套
        /// （<c>AdofaiSettings.KeyAnchor</c>），工作区吸附整体让位；否则用
        /// <c>KvAppSettings.Anchor</c>。设置面板的九宫格必须按这个结果去读写，
        /// 不然就会出现「改了没反应」——用户看到的症状是「选了右下角却被吸到中间」。
        /// </para>
        /// </summary>
        public static int EffectiveKeyAnchor(bool snapToGame, int workAnchor, int gameAnchor)
            => Clamp(snapToGame ? gameAnchor : workAnchor);

        /// <summary>当前工作区（DIP）。任务栏位置、分辨率变化都会立刻反映到这里。</summary>
        public static Rect WorkArea => SystemParameters.WorkArea;

        /// <summary>工作区发生了变化（分辨率 / 任务栏 / 缩放）。</summary>
        public static bool WorkAreaChanged(Rect cached, Rect now)
        {
            const double eps = 0.5;
            return System.Math.Abs(cached.Left - now.Left) > eps ||
                   System.Math.Abs(cached.Top - now.Top) > eps ||
                   System.Math.Abs(cached.Width - now.Width) > eps ||
                   System.Math.Abs(cached.Height - now.Height) > eps;
        }

        /// <summary>
        /// 算出左上角。<paramref name="anchor"/> 为 0（自由）时返回 false，不修改输出。
        /// 结果会被夹在工作区内，保证整块至少和一个角落在屏幕里（依旧不改大小）。
        /// </summary>
        public static bool Place(int anchor, double margin, double w, double h,
                                 Rect area, out double left, out double top)
        {
            left = area.Left;
            top = area.Top;

            anchor = Clamp(anchor);
            if (anchor == 0) return false;
            if (margin < 0) margin = 0;
            if (w < 0) w = 0;
            if (h < 0) h = 0;

            int col = (anchor - 1) % 3;
            int row = (anchor - 1) / 3;

            double maxLeft = area.Right - w;
            double maxTop = area.Bottom - h;

            left = col == 0 ? area.Left + margin
                 : col == 1 ? area.Left + (area.Width - w) * 0.5
                            : maxLeft - margin;

            top = row == 0 ? area.Top + margin
                : row == 1 ? area.Top + (area.Height - h) * 0.5
                           : maxTop - margin;

            // 块比工作区还大时 maxLeft 会跑到 area.Left 左边，这时退回 area.Left，
            // 让左上角可见（用户仍然能靠拖动或改尺寸处理）。
            if (left < area.Left) left = area.Left;
            else if (left > maxLeft) left = maxLeft < area.Left ? area.Left : maxLeft;

            if (top < area.Top) top = area.Top;
            else if (top > maxTop) top = maxTop < area.Top ? area.Top : maxTop;

            return true;
        }
    }
}

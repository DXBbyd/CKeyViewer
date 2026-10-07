namespace CKeyViewer.Core
{
    /// <summary>
    /// 与 jipper `config/settings.json` **逐字段兼容**的元数据。
    /// 只保存「当前用哪个档案 / 有哪些档案」这类信息，具体配置在 profiles/&lt;name&gt;.json。
    /// </summary>
    public sealed class KvAppSettings
    {
        /// <summary>配置版本。原版当前为 6。</summary>
        public int Version { get; set; } = 6;

        public string CurrentProfile { get; set; } = "Default";

        public string[] ProfileNames { get; set; } = { "Default" };

        /// <summary>"en" / "zh" —— 原版的语言标记。</summary>
        public string Language { get; set; } = "zh";

        /// <summary>原版设置面板记忆的标签页下标。</summary>
        public int UiTab { get; set; }

        /// <summary>
        /// 布局模式下是否允许用方向键微调选中节点。
        /// 默认关闭 —— 方向键常常被游戏 / 浏览器占用，一路按下去会把节点悄悄带偏。
        /// </summary>
        public bool ArrowNudge { get; set; }

        /// <summary>
        /// 「拖拽为只」：开启后自由布局编辑器隐藏坐标数字输入，节点位置只能靠在屏幕上拖拽。
        /// 与「引导创建按键」流程共享同一个隐藏坐标的行为。
        /// </summary>
        public bool DragOnlyPosition { get; set; }

        /// <summary>
        /// 「按住热键拖窗口」用到的虚拟键码（0 = 关闭该功能）。
        /// 默认 <c>VK_MENU</c>(Alt)：按住 Alt 再用鼠标左键拖动覆盖层，即可把整套预设键位
        /// 放到屏幕任意位置（全屏范围内移动），松手自动落盘。
        /// <para>
        /// 放在 settings.json（而非档案）：这是「这台机器上怎么操作」，跟用哪套键位无关，
        /// 也免得往 profiles/*.json 里加字段破坏与 jipper 的双向兼容。
        /// </para>
        /// </summary>
        public int DragHotkeyVk { get; set; } = 0x12;

        /// <summary>
        /// 界面主题：<c>"dark"</c> / <c>"light"</c>。
        ///
        /// 放在 settings.json 而不是档案里 —— 这是「这台机器上界面长什么样」，
        /// 跟用哪个按键档案无关。jipper 读这个文件会忽略不认识的字段，不会互相干扰。
        /// </summary>
        public string Theme { get; set; } = "dark";

        /// <summary>
        /// 按键覆盖层窗口的吸附锚点（<see cref="KvAnchor"/>）。0 = 自由 ——
        /// 那时位置由「布局」页的自定义位置 / 默认贴底居中决定。
        /// 放在这里而不是档案里：它是「这块屏幕上怎么摆」，跟用哪套键位无关，
        /// 也免得往 profiles/*.json 里加字段破坏与 jipper 的双向兼容。
        /// </summary>
        public int Anchor { get; set; }

        /// <summary>吸附时离工作区边缘留出的空隙（DIP）。</summary>
        public double AnchorMargin { get; set; } = 12;

        /// <summary>吸附是否在窗口 / 分辨率变化后自动重算（动态吸附）。</summary>
        public bool AnchorDynamic { get; set; } = true;

        public void Sanitize()
        {
            if (Version <= 0) Version = 6;
            if (string.IsNullOrWhiteSpace(Language)) Language = "zh";
            if (ProfileNames == null || ProfileNames.Length == 0)
                ProfileNames = new[] { "Default" };
            if (string.IsNullOrWhiteSpace(CurrentProfile))
                CurrentProfile = ProfileNames[0];
            if (UiTab < 0) UiTab = 0;
            if (Theme != "light" && Theme != "dark") Theme = "dark";
            Anchor = KvSnap.Clamp(Anchor);
            if (double.IsNaN(AnchorMargin) || double.IsInfinity(AnchorMargin)) AnchorMargin = 12;
            AnchorMargin = System.Math.Max(0, System.Math.Min(400, AnchorMargin));
            if (DragHotkeyVk < 0) DragHotkeyVk = 0x12;
        }
    }
}

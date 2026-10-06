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

        public void Sanitize()
        {
            if (Version <= 0) Version = 6;
            if (string.IsNullOrWhiteSpace(Language)) Language = "zh";
            if (ProfileNames == null || ProfileNames.Length == 0)
                ProfileNames = new[] { "Default" };
            if (string.IsNullOrWhiteSpace(CurrentProfile))
                CurrentProfile = ProfileNames[0];
            if (UiTab < 0) UiTab = 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CKeyViewer.Core;
using CKeyViewer.Native;
using CKeyViewer.Render;

namespace CKeyViewer
{
    /// <summary>
    /// 主控制器：持有配置档案、布局与按键状态，每帧轮询输入并驱动渲染。
    /// 所有可调参数都来自 <see cref="KvProfile"/>，与 jipper 配置文件一一对应。
    /// </summary>
    public sealed class KvHost
    {
        private readonly OverlayWindow _window;
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _saveTimer;
        private readonly Dictionary<int, KeyRuntime> _keys = new Dictionary<int, KeyRuntime>();
        private List<KeySlot> _slots = new List<KeySlot>();

        /// <summary>配置仓库（settings.json + profiles/*.json）。</summary>
        public readonly KvProfileStore Store;

        /// <summary>当前档案的快捷访问。</summary>
        public KvProfile P => Store.Profile;

        /// <summary>累计按键总数 —— 与原版一致，由 profile.TotalCount 延续并逐次 +1。</summary>
        private long _totalCount;

        /// <summary>主要键位（Unity KeyCode），下标即键槽 Index。</summary>
        public int[] KeyCodes = { 97, 115, 100, 102, 106, 107, 108, 59, 118, 99, 98, 110, 122, 120, 109, 44 };

        /// <summary>脚键（Unity KeyCode）。当前样式未启用脚键时为空。</summary>
        public int[] FootKeyCodes = Array.Empty<int>();

        public KvTheme Theme = new KvTheme();
        public double KeyFontSize = 21;
        public string FontRef = "Segoe UI";
        public bool FontBold = true;
        public bool FontItalic;

        private static bool RainRowEnabled(KvProfile p, int row)
        {
            switch (row)
            {
                case 1: return p.EnableRainForRow2;
                case 2: return p.EnableRainForRow3;
                default: return p.EnableRainForRow1;
            }
        }

        public KvHost(OverlayWindow window, KvProfileStore store)
        {
            _window = window;
            Store = store ?? KvProfileStore.CreateDefault();
            Rain = new KvRainLayer(Store);

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(8)
            };
            _timer.Tick += (s, e) => Tick();

            // 每 5 秒把计数落盘一次，避免异常退出丢进度
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _saveTimer.Tick += (s, e) => FlushStats();

            // 设置面板改动后的延迟落盘（避免拖动滑块时疯狂写盘）
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _debounce.Tick += (s, e) =>
            {
                _debounce.Stop();
                if (!_dirty) return;
                _dirty = false;
                SavePendingStats();
                Store.SaveAll();
            };

            // 布局模式：每 16ms 判断鼠标是否压在节点上，据此动态开关鼠标穿透；
            // 同时轮询 Esc / 方向键 / 左键拖动 —— 覆盖层是 WS_EX_NOACTIVATE + 分层窗口，
            // 既收不到键盘消息，WPF 的鼠标投递也不可靠，统统一律走 GetAsyncKeyState 轮询。
            _hitTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _hitTimer.Tick += (s, e) => HitTick();
        }

        private readonly DispatcherTimer _debounce;
        private bool _dirty;

        /// <summary>标记档案已改动，稍后自动落盘。</summary>
        public void QueueSave()
        {
            _dirty = true;
            _debounce.Stop();
            _debounce.Start();
        }

        /// <summary>立即落盘（关闭设置面板时调用）。</summary>
        public void FlushNow()
        {
            _debounce.Stop();
            _dirty = false;
            FlushStats();
            Store.SaveSettings();
        }

        /// <summary>雨线层 —— 触发/释放由本控制器负责，推进与绘制在层内完成。</summary>
        public readonly KvRainLayer Rain;

        public void Start()
        {
            Store.Load();
            ApplyProfile();
            _timer.Start();
            _saveTimer.Start();
            ScheduleDiagnostics();
        }

        public void Stop()
        {
            if (LayoutMode) SetLayoutMode(false);
            _hitTimer?.Stop();
            _timer.Stop();
            _saveTimer.Stop();
            FlushStats();
            Store.SaveSettings();
        }

        // ---------------------------------------------------------------
        // 配置 → 运行时
        // ---------------------------------------------------------------

        /// <summary>把当前档案的字段全部映射到渲染器与窗口。</summary>
        public void ApplyProfile()
        {
            var p = P;
            p.Sanitize();

            var style = p.StyleEnum;
            KeyCodes = p.KeysFor(style) ?? KvProfile.DefaultKeysFor(style);

            var footStyle = p.FootStyleEnum;
            FootKeyCodes = footStyle == FootKeyviewerStyle.None
                ? Array.Empty<int>()
                : (p.FootKeysFor(footStyle) ?? Array.Empty<int>());

            Theme = KvTheme.FromProfile(p);
            KeyFontSize = p.KeyFontSize;

            string refName = p.FontName;
            FontRef = KvFonts.ResolveFontRef(refName, p.FontStyleFlags, out bool bold, out bool italic);
            FontBold = bold;
            FontItalic = italic;

            _totalCount = p.TotalCount;

            Rebuild();
        }

        // ---------------------------------------------------------------
        // 诊断
        // ---------------------------------------------------------------

        private void ScheduleDiagnostics()
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                DumpDiagnostics();
            };
            t.Start();
        }

        private void DumpDiagnostics()
        {
            var w = _window;
            Diag.Log(string.Format("WPF Left={0:0.##} Top={1:0.##} W={2:0.##} H={3:0.##} IsVisible={4}",
                w.Left, w.Top, w.Width, w.Height, w.IsVisible));
            Diag.Log(string.Format("Actual={0:0.##}x{1:0.##} renderer={2:0.##}x{3:0.##}",
                w.ActualWidth, w.ActualHeight, w.Renderer.ActualWidth, w.Renderer.ActualHeight));
            Diag.Log(string.Format("slots={0} keys={1} renders={2} scale={3:0.###} topExtent={4:0.##} originX={5:0.##}",
                _slots.Count, _keys.Count, w.Renderer.RenderCount, w.Renderer.Scale,
                w.Renderer.TopExtent, w.Renderer.OriginX));
            Diag.Log(string.Format("profile={0} style={1} down={2} stdW={3} font={4}({5}/{6}) total={7}",
                Store.CurrentProfile, P.KeyViewerStyle, P.DownLocation, P.StandardKeyWidth,
                FontRef, FontBold, FontItalic, _totalCount));
            Diag.Log(string.Format("layout={0} selected={1} clickThrough={2} esc={3} lbtn={4}",
                LayoutMode, SelectedNodeId, _window.ClickThrough,
                Win32.IsKeyDown(Win32.VK_ESCAPE), Win32.IsKeyDown(Win32.VK_LBUTTON)));

            IntPtr hwnd = w.Handle;
            if (hwnd != IntPtr.Zero)
            {
                Win32.GetWindowRect(hwnd, out int l, out int tt, out int rr, out int b);
                Diag.Log(string.Format("win32 rect=({0},{1})-({2},{3}) {4}x{5} visible={6} ex=0x{7:X8}",
                    l, tt, rr, b, rr - l, b - tt, Win32.IsWindowVisible(hwnd), Win32.GetExStyle(hwnd)));
            }
            else
            {
                Diag.Log("hwnd = 0");
            }
        }

        private static double Now()
        {
            return Environment.TickCount64 / 1000.0;
        }

        // ---------------------------------------------------------------
        // 布局重建
        // ---------------------------------------------------------------

        public void Rebuild()
        {
            var style = P.StyleEnum;
            bool custom = KvGeometry.IsCustom(style);

            // 自由布局：键槽来自 CustomNodes，坐标是画布绝对坐标
            _slots = custom
                ? KvGeometry.BuildCustomSlots(P)
                : KvGeometry.BuildSlots(style, P.StandardKeyWidth, P.DownLocation);

            // 脚键与主键同处一个覆盖层（原版就挂在同一个 KeyViewerSizeObject 下），
            // 全键盘与自定义布局不显示脚键。
            if (!custom && FootKeyCodes.Length > 0 && !KvGeometry.IsFullKeyboard(style))
                _slots.AddRange(KvGeometry.BuildFootSlots(P.FootStyleEnum));

            _keys.Clear();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Index < 0) continue;
                if (_keys.ContainsKey(slot.Index)) continue;   // 同一键位多槽共享计数

                _keys[slot.Index] = slot.Node != null
                    ? BuildCustomKey(slot)
                    : BuildPresetKey(slot);
            }

            var metrics = KvGeometry.Measure(_slots);

            double contentTop = Math.Max(TopExtentOf(_slots), 0);
            double minY = MinBottomOf(_slots);

            // 雨线向上能盖到的最高点：基线 + 2 倍轨道高（生长 1 倍 + 上升 1 倍）
            double rainTop = RainTopReach(_slots);
            double topExtent = Math.Max(contentTop, rainTop);

            // 自由布局下窗口铺满整屏，画布坐标 → 屏幕坐标一一对应
            if (custom)
            {
                metrics = new BlockMetrics
                {
                    Left = 0f,
                    Width = (float)(KvGeometry.CanvasHeight * ScreenAspect()),
                    Height = KvGeometry.CanvasHeight
                };
                topExtent = KvGeometry.CanvasHeight;
            }

            double blockWidth = metrics.Width;
            double blockHeight = custom ? KvGeometry.CanvasHeight : Math.Max(1, topExtent - minY);

            // 参考单位 → DIP 的换算（含 DPI 与用户 Size）
            double scale = ComputeScale();

            var r = _window.Renderer;
            r.Slots = _slots;
            r.Keys = _keys;
            r.Styles = BuildKeyStyles();
            r.IsCustomLayout = custom;
            r.LayoutMode = LayoutMode;
            r.SelectedNode = P.NodeById(SelectedNodeId);
            r.ArrowNudgeHint = Store.Settings.ArrowNudge;
            r.Theme = Theme;
            r.Scale = scale;
            r.OriginX = metrics.Left;
            r.TopExtent = topExtent;
            r.BlockWidth = blockWidth;
            r.BlockHeight = blockHeight;
            r.KeyFontSize = KeyFontSize;
            r.CountFontSize = Math.Max(10, KeyFontSize * 0.62);
            r.FontRef = FontRef;
            r.Bold = FontBold;
            r.Italic = FontItalic;
            r.CountFormatting = P.EnableCountFormatting;
            r.HideMainKeyCount = P.HideMainKeyCount;
            r.KpsLabel = string.IsNullOrEmpty(P.KpsLabel) ? "KPS" : P.KpsLabel;
            r.TotalLabel = string.IsNullOrEmpty(P.TotalLabel) ? "Total" : P.TotalLabel;
            r.HideKpsTotalLabel = P.HideKpsTotalLabel;
            r.StreamerMode = P.StreamerMode;
            r.RainEnabled = P.EnableRainEffect;
            r.Rain = Rain;
            r.EnablePressAnimation = P.EnablePressAnimation;
            r.AnimAffectsRain = P.EnablePressAnimationOnRain;
            r.RainPadding = topExtent - contentTop;

            ApplyWindowGeometry(blockWidth, blockHeight, scale);
        }

        /// <summary>预设布局的按键（键码来自 key* 数组，计数落在全局 Count 数组里）。</summary>
        private KeyRuntime BuildPresetKey(KeySlot slot)
        {
            bool foot = slot.IsFootKey;
            int code = foot
                ? (slot.Index - KvGeometry.FootKeyBase < FootKeyCodes.Length
                    ? FootKeyCodes[slot.Index - KvGeometry.FootKeyBase] : 0)
                : (slot.Index < KeyCodes.Length ? KeyCodes[slot.Index] : 0);

            int ghost = foot ? 0 : GhostCodeFor(slot.Index);

            var key = new KeyRuntime
            {
                SlotIndex = slot.Index,
                IsFootKey = foot,
                UnityKeyCode = code,
                Vk = KeyCodeMap.ToVirtualKey(code),
                GhostUnityKeyCode = ghost,
                GhostVk = KeyCodeMap.ToVirtualKey(ghost),

                // -1 = 沿用原版的按下标推导行号
                RainRow1 = -1,

                // 键名优先用配置里的 key*Text / footkey*Text，空串则回落到键码的标准显示名
                Label = LabelFor(slot.Index, foot)
            };

            // 延续原版档案里的累计次数（Count 数组固定 40 项，脚键占 24..39）
            if (slot.Index < P.Count.Length) key.Count = P.Count[slot.Index];
            return key;
        }

        /// <summary>
        /// 自由布局节点的按键。键码由节点的 <c>KeyBind</c>（Unity KeyCode 枚举名）解析，
        /// 计数存在节点自己的 <c>Count</c> 上（原版 <c>node.Count++</c>）。
        /// </summary>
        private KeyRuntime BuildCustomKey(KeySlot slot)
        {
            var n = slot.Node;
            int code = KeyCodeMap.FromName(n.KeyBind);
            int ghost = KeyCodeMap.FromName(n.GhostKey);

            return new KeyRuntime
            {
                SlotIndex = slot.Index,
                IsFootKey = false,
                UnityKeyCode = code,
                Vk = KeyCodeMap.ToVirtualKey(code),
                GhostUnityKeyCode = ghost,
                GhostVk = KeyCodeMap.ToVirtualKey(ghost),

                // RainEnabled 关掉时给 0，雨线层据此直接跳过
                RainRow1 = n.RainEnabled ? Math.Clamp(n.RainRow, 0, 2) + 1 : 0,

                Label = string.IsNullOrEmpty(n.CustomText)
                        ? KeyCodeMap.DisplayName(code)
                        : n.CustomText,
                PressedLabel = n.PressedText ?? "",

                CountInTotal = n.CountInTotal,
                Count = n.Count
            };
        }

        /// <summary>画面宽高比 —— 原版 CanvasWidth = Screen.width * 1080 / Screen.height。</summary>
        private double ScreenAspect()
        {
            Native.Win32.GetPrimaryScreenPixels(out int sw, out int sh);
            if (sw <= 0 || sh <= 0) return 16.0 / 9.0;
            return (double)sw / sh;
        }

        /// <summary>
        /// 预解析每个键槽的最终配色 —— 把「每键配色 / 全键盘统一配色 / 全局配色」三种
        /// 优先级在这里一次性判定完，渲染时只查表。
        /// </summary>
        private Dictionary<int, KeyStyle> BuildKeyStyles()
        {
            var p = P;
            var map = new Dictionary<int, KeyStyle>(_keys.Count);
            bool full = KvGeometry.IsFullKeyboard(p.StyleEnum);

            foreach (var slot in _slots)
            {
                int idx = slot.Index;
                if (map.ContainsKey(idx)) continue;

                // 统计条没有 KeyRuntime；预设布局的统计条直接用全局 KPS/Total 配色，
                // 但自由布局的统计条节点要能单独配色，所以这里放行。
                if (idx < 0 && slot.Node == null) continue;
                if (idx >= 0 && !_keys.ContainsKey(idx)) continue;

                bool foot = slot.IsFootKey;
                var node = slot.Node;

                KeyStyle st;

                if (node != null)
                {
                    // 自由布局节点：UseCustomColor 打开时用节点自带配色，否则沿用全局
                    st = node.UseCustomColor
                        ? KeyStyle.From(
                            NodeColor(node.Bg, p.Background),
                            NodeColor(node.BgPressed, p.BackgroundClicked),
                            NodeColor(node.Outline, p.Outline),
                            NodeColor(node.OutlinePressed, p.OutlineClicked),
                            NodeColor(node.TextColor, p.Text),
                            NodeColor(node.TextColorPressed, p.TextClicked))
                        : KeyStyle.From(
                            p.Background, p.BackgroundClicked,
                            p.Outline, p.OutlineClicked,
                            p.Text, p.TextClicked);

                    if (node.FontSize > 0f) st.FontSize = node.FontSize;
                    if (node.CornerRadius > 0f) st.CornerRadius = node.CornerRadius;
                    if (node.BorderThickness > 0f) st.BorderThickness = node.BorderThickness;

                    st.Opacity = Math.Clamp(node.Opacity <= 0f ? 1f : node.Opacity, 0.02f, 1f);
                    st.HideLabel = node.HideLabel;
                    st.ShowCount = !node.HideCount && !node.IsStat;
                    map[idx] = st;
                    continue;
                }

                if (p.EnablePerKeyColors && idx >= 0 && idx < KvProfile.PerKeySlots
                    && p.PerKeyBackground != null && idx < p.PerKeyBackground.Count)
                {
                    st = KeyStyle.From(
                        p.PerKeyBackground[idx],
                        Pick(p.PerKeyBackgroundClicked, idx, p.BackgroundClicked),
                        Pick(p.PerKeyOutline, idx, p.Outline),
                        Pick(p.PerKeyOutlineClicked, idx, p.OutlineClicked),
                        Pick(p.PerKeyText, idx, p.Text),
                        Pick(p.PerKeyTextClicked, idx, p.TextClicked));
                }
                else if (full && p.EnableFullKeyboardUnifiedColor)
                {
                    st = KeyStyle.From(
                        p.FullKeyboardBackground, p.FullKeyboardBackgroundClicked,
                        p.FullKeyboardOutline, p.FullKeyboardOutlineClicked,
                        p.FullKeyboardText, p.FullKeyboardTextClicked);
                }
                else
                {
                    st = KeyStyle.From(
                        p.Background, p.BackgroundClicked,
                        p.Outline, p.OutlineClicked,
                        p.Text, p.TextClicked);
                }

                if (p.EnablePerKeyTextSize && p.PerKeyFontSize != null
                    && idx >= 0 && idx < p.PerKeyFontSize.Count && p.PerKeyFontSize[idx] > 0f)
                {
                    st.FontSize = p.PerKeyFontSize[idx];
                }

                // 脚键不显示计数（原版 CreateKey(count: false)）
                st.ShowCount = !foot;
                map[idx] = st;
            }

            return map;
        }

        private static KvColor Pick(List<KvColor> list, int idx, KvColor fallback)
        {
            if (list == null || idx < 0 || idx >= list.Count) return fallback;
            return list[idx];
        }

        /// <summary>
        /// 自由布局节点的颜色数组 → KvColor。原版 <c>NodeColor</c> 要求长度**恰好为 4**
        /// （<c>[r,g,b,a]</c>，0~1），否则回落到全局色。
        /// </summary>
        public static KvColor NodeColor(float[] arr, KvColor fallback)
        {
            if (arr == null || arr.Length != 4) return fallback;
            return KvColor.Rgba(
                Math.Clamp(arr[0], 0f, 1f),
                Math.Clamp(arr[1], 0f, 1f),
                Math.Clamp(arr[2], 0f, 1f),
                Math.Clamp(arr[3], 0f, 1f));
        }

        /// <summary>KvColor → 节点颜色数组（编辑器写回用）。</summary>
        public static float[] ToNodeColor(KvColor c)
            => new[] { c.r, c.g, c.b, c.a };

        /// <summary>
        /// 雨线能到达的最高点（参考坐标）。原版的基线位于
        /// <c>键底边 + RainStartY + 275</c>，条身最高可再长 275 并上升 275，
        /// 所以取 <c>键底边 + RainStartY + 2 × RainHeight</c>；各行参数已刻意配平到同一条线。
        /// </summary>
        private double RainTopReach(List<KeySlot> slots)
        {
            if (!P.EnableRainEffect || KvGeometry.IsFullKeyboard(P.StyleEnum))
                return double.MinValue;

            double max = double.MinValue;
            foreach (var s in slots)
            {
                if (s.Index < 0) continue;
                if (s.IsFootKey) continue;   // 脚键无雨线

                int row;
                if (s.Node != null)
                {
                    if (!s.Node.RainEnabled) continue;    // 节点自己关掉了雨线
                    row = Math.Clamp(s.Node.RainRow, 0, 2);
                }
                else
                {
                    row = KvRainLayer.RowOf(s.Index);
                }

                if (!RainRowEnabled(P, row)) continue;

                double start;
                double height;
                switch (row)
                {
                    case 1: start = P.RainStartYRow2; height = P.RainHeightRow2; break;
                    case 2: start = P.RainStartYRow3; height = P.RainHeightRow3; break;
                    default: start = P.RainStartYRow1; height = P.RainHeightRow1; break;
                }

                double reach = s.Bottom + start + KvRainLayer.ContainerHeight + Math.Max(height, 1);
                if (reach > max) max = reach;
            }
            return max;
        }

        /// <summary>鬼键（同一键帽上绑定的第二个按键）的键码，0 表示未绑定。</summary>
        private int GhostCodeFor(int index)
        {
            int[] ghosts;
            switch (P.StyleEnum)
            {
                case KeyviewerStyle.Key8: ghosts = P.GhostKey8; break;
                case KeyviewerStyle.Key10: ghosts = P.GhostKey10; break;
                case KeyviewerStyle.Key12: ghosts = P.GhostKey12; break;
                case KeyviewerStyle.Key14: ghosts = P.GhostKey14; break;
                case KeyviewerStyle.Key16: ghosts = P.GhostKey16; break;
                case KeyviewerStyle.Key20: ghosts = P.GhostKey20; break;
                case KeyviewerStyle.Key24: ghosts = P.GhostKey24; break;
                default: return 0;
            }
            if (ghosts == null || index < 0 || index >= ghosts.Length) return 0;
            return ghosts[index];
        }

        /// <summary>
        /// 原版画布：**高度恒为 1080**，宽度按屏幕宽高比算出
        /// （<c>CanvasWidth = Screen.width * 1080 / Screen.height</c>）。
        /// 布局尺寸在画布坐标里是固定值，<c>Size</c> 是整体缩放。
        /// 因此：1 参考单位 = 屏幕物理高 / 1080 × Size 个物理像素，
        /// 再除以 DPI 缩放就是 WPF 用的 DIP。
        /// </summary>
        private double ComputeScale()
        {
            Native.Win32.GetPrimaryScreenPixels(out _, out int screenHpx);
            double unitPx = screenHpx / KvGeometry.ReferenceHeight;
            double sizePx = unitPx * Math.Max(0.05, P.Size);
            return sizePx / _window.DpiScale;
        }

        /// <summary>取配置里该键槽的自定义键名。</summary>
        private string LabelFor(int index, bool foot)
        {
            string[] texts;
            int i;

            if (foot)
            {
                texts = P.FootKeyTextsFor(P.FootStyleEnum);
                i = index - KvGeometry.FootKeyBase;
            }
            else
            {
                texts = P.KeyTextsFor(P.StyleEnum);
                i = index;
            }

            if (texts != null && i >= 0 && i < texts.Length && !string.IsNullOrEmpty(texts[i]))
                return texts[i];

            return null;
        }

        private static double TopExtentOf(List<KeySlot> slots)
        {
            double max = double.MinValue;
            foreach (var s in slots) if (s.Top > max) max = s.Top;
            return max == double.MinValue ? 0 : max;
        }

        private static double MinBottomOf(List<KeySlot> slots)
        {
            double min = double.MaxValue;
            foreach (var s in slots) if (s.Bottom < min) min = s.Bottom;
            return min == double.MaxValue ? 0 : min;
        }

        /// <summary>
        /// 按原版的定位公式摆放窗口。
        /// <para>
        /// 画布高度恒 1080；<c>MainKeyViewerPosition</c> 是 0..1 归一化坐标：
        /// y=1 时区块底边贴屏幕底边，y=0 时区块顶边贴屏幕顶边；
        /// x=0 贴左、x=1 贴右。未启用自定义位置时默认贴底并水平居中。
        /// </para>
        /// </summary>
        private void ApplyWindowGeometry(double blockWidth, double blockHeight, double scale)
        {
            double dpi = _window.DpiScale;

            double wDip = Math.Max(1, blockWidth * scale);
            double hDip = Math.Max(1, blockHeight * scale);

            if (P.CustomPositionEnabled && !KvGeometry.IsCustom(P.StyleEnum))
            {
                // 自定义位置：完全按原版的归一化画布公式（0..1 映射到整块屏幕）
                double nx = Math.Clamp(P.MainKeyViewerPosition.x, 0f, 1f);
                double ny = Math.Clamp(P.MainKeyViewerPosition.y, 0f, 1f);

                Native.Win32.GetPrimaryScreenPixels(out int screenWpx, out int screenHpx);

                double wPx = wDip * dpi;
                double hPx = hDip * dpi;

                double leftPx = nx * Math.Max(0, screenWpx - wPx);
                double bottomPx = (1.0 - ny) * Math.Max(0, screenHpx - hPx);
                double topPx = screenHpx - bottomPx - hPx;

                _window.Left = leftPx / dpi;
                _window.Top = topPx / dpi;
            }
            else if (KvGeometry.IsCustom(P.StyleEnum))
            {
                // 自由布局：窗口铺满整屏。画布高度恒为 1080，画布坐标
                // （X 自左边、Y 自底边）→ 屏幕坐标因此是一一对应的，
                // 与「Size」缩放一并生效（原版 KeyViewerSizeObject.localScale）。
                _window.Left = 0;
                _window.Top = 0;
            }
            else
            {
                // 默认位置：水平居中、贴在工作区底边（自动避开任务栏，
                // 免得按键压在任务栏图标上）。坐标本身就是 DIP，直接用。
                var work = System.Windows.SystemParameters.WorkArea;

                _window.Left = work.Left + Math.Max(0, (work.Width - wDip) * 0.5);
                _window.Top = Math.Max(work.Top, work.Bottom - hDip);
            }

            _window.Width = wDip;
            _window.Height = hDip;

            Diag.Log(string.Format(
                "geom block={0:0.##}x{1:0.##} ref->dip={2:0.####} custom={3} dpi={4:0.###} " +
                "-> dip L={5:0.##} T={6:0.##} W={7:0.##} H={8:0.##}",
                blockWidth, blockHeight, scale, P.CustomPositionEnabled, dpi,
                _window.Left, _window.Top, _window.Width, _window.Height));
        }

        // ---------------------------------------------------------------
        // 每帧
        // ---------------------------------------------------------------

        private void Tick()
        {
            double now = Now();

            bool pressedChanged = false;
            bool animating = false;

            bool animEnabled = P.EnablePressAnimation;
            double animDur = Math.Max(10.0, Math.Min(2000.0, P.PressAnimationDurationMs)) / 1000.0;
            string animEase = KvEasing.Normalize(P.PressAnimationEasing);

            foreach (var kv in _keys)
            {
                var key = kv.Value;

                bool down = key.Vk != 0 && Win32.IsKeyDown(key.Vk);
                key.Pressed = down;

                if (down != key.WasPressed)
                {
                    if (down)
                    {
                        key.RecordPress(now);
                        if (key.CountInTotal) _totalCount++;
                        Rain.Trigger(key, now);
                    }
                    else
                    {
                        Rain.Release(key, now);
                    }

                    // 按压动画：按下缩到 PressAnimationScale，松开回到 1
                    if (animEnabled) key.StartAnim(down ? P.PressAnimationScale : 1.0, now);

                    pressedChanged = true;
                }
                key.WasPressed = down;

                // 鬼键：只触发雨线，不计数（原版行为）
                if (key.GhostVk != 0)
                {
                    bool ghostDown = Win32.IsKeyDown(key.GhostVk);
                    key.GhostPressed = ghostDown;
                    if (ghostDown != key.GhostWasPressed)
                    {
                        if (ghostDown) Rain.TriggerGhost(key, now);
                        else Rain.ReleaseGhost(key, now);
                        pressedChanged = true;
                    }
                    key.GhostWasPressed = ghostDown;
                }

                if (key.KpsLog.Count > 0) key.TrimKpsLog(now);

                if (animEnabled)
                {
                    key.AdvanceAnim(now, animDur, animEase);
                    if (Math.Abs(key.AnimScale - key.AnimTo) > 0.0005) animating = true;
                }
            }

            // 雨线推进；有活跃雨线时即使按键状态没变也必须重绘
            bool raining = Rain.Update(_keys.Values, now);

            var r = _window.Renderer;
            r.Now = now;
            r.TotalCount = _totalCount;
            r.TotalKps = TotalKps;

            // KPS 是 1 秒滑动窗口，需要持续刷新；空闲时降到低频重绘省 CPU
            bool kpsAlive = false;
            foreach (var kv in _keys)
            {
                if (kv.Value.KpsLog.Count > 0) { kpsAlive = true; break; }
            }

            if (raining || kpsAlive || pressedChanged || animating || now - _lastPaint >= IdlePaintInterval)
            {
                _lastPaint = now;
                r.Invalidate();
            }
        }

        private double _lastPaint;
        private const double IdlePaintInterval = 0.5;

        public long TotalCount => _totalCount;

        public int TotalKps
        {
            get
            {
                int total = 0;
                foreach (var kv in _keys) total += kv.Value.Kps;
                return total;
            }
        }

        // ---------------------------------------------------------------
        // 计数落盘
        // ---------------------------------------------------------------

        /// <summary>把运行时计数写回档案并保存（Count 数组固定 40 项）。</summary>
        public void FlushStats()
        {
            if (P == null) return;

            if (KvGeometry.IsCustom(P.StyleEnum))
            {
                // 自由布局：计数存在节点自己的 Count 上（原版 node.Count）。
                // **不能**写进预设的 Count[] —— 自定义布局的槽位下标是从 0 顺延的，
                // 会把预设布局的计数整个串掉。
                foreach (var slot in _slots)
                {
                    var n = slot.Node;
                    if (n == null) continue;
                    if (!_keys.TryGetValue(slot.Index, out var k)) continue;
                    n.Count = (int)Math.Min(int.MaxValue, k.Count);
                }
            }
            else
            {
                var counts = P.Count;
                if (counts == null || counts.Length != 40)
                {
                    counts = new int[40];
                    P.Count = counts;
                }

                foreach (var kv in _keys)
                {
                    int idx = kv.Key;
                    if (idx < 0 || idx >= counts.Length) continue;
                    counts[idx] = (int)Math.Min(int.MaxValue, kv.Value.Count);
                }
            }

            P.TotalCount = _totalCount;
            Store.SaveProfile();
        }

        /// <summary>把所有键的计数归零（原版 ResetCounts）。</summary>
        public void ResetCounts()
        {
            foreach (var kv in _keys) kv.Value.ResetCounts();
            _totalCount = 0;
            Rain.ClearAll(_keys.Values);
            FlushStats();
            _window.Renderer.Invalidate();
        }

        // ---------------------------------------------------------------
        // 布局模式（在覆盖层上直接拖动节点）
        // ---------------------------------------------------------------

        private readonly DispatcherTimer _hitTimer;
        private KvFmNode _dragNode;
        private double _dragOffX, _dragOffY;
        private bool _dragged;
        private bool _lbtnWasDown;
        private bool _escWasDown;
        private readonly bool[] _nudgeWas = new bool[4];
        private readonly double[] _nudgeNext = new double[4];

        /// <summary>布局模式开关。</summary>
        public bool LayoutMode { get; private set; }

        /// <summary>布局模式下选中的节点 id（0 = 未选中）。</summary>
        public int SelectedNodeId { get; private set; }

        /// <summary>布局模式状态或选中项变化时触发（设置面板据此刷新）。</summary>
        public event Action LayoutStateChanged;

        /// <summary>当前画布宽度（参考单位）：1080 × 屏幕宽高比。</summary>
        public double CanvasWidthRef => KvGeometry.CanvasHeight * ScreenAspect();

        /// <summary>
        /// 进出布局模式。<para>
        /// 进入时：若当前不是自由布局会自动切到自由布局；覆盖层临时接受鼠标
        /// （只对「压在节点上」的位置生效，别处照旧穿透，不会挡住桌面操作）。</para>
        /// </summary>
        public void SetLayoutMode(bool on, string reason = null)
        {
            if (LayoutMode == on) return;

            LayoutMode = on;

            if (on)
            {
                if (!KvGeometry.IsCustom(P.StyleEnum))
                {
                    P.KeyViewerStyle = (int)KeyviewerStyle.Custom;
                    ReloadFromProfile();
                }

                if (SelectedNodeId <= 0 && P.CustomNodes.Count > 0)
                    SelectedNodeId = P.CustomNodes[0].Id;

                if (!_window.IsVisible) Show();

                _escWasDown = Win32.IsKeyDown(Win32.VK_ESCAPE);
                _dragNode = null;
                _hitTimer.Start();
            }
            else
            {
                _hitTimer.Stop();
                _dragNode = null;
                _lbtnWasDown = Win32.IsKeyDown(Win32.VK_LBUTTON);
                _window.ClickThrough = true;
                FlushStats();
                Store.SaveProfile();
            }

            var r = _window.Renderer;
            r.LayoutMode = on;
            r.SelectedNode = P.NodeById(SelectedNodeId);
            r.ArrowNudgeHint = Store.Settings.ArrowNudge;
            r.Invalidate();

            Diag.Log("layout mode = " + on + " (" + (reason ?? "?") + ") selected=" + SelectedNodeId);
            LayoutStateChanged?.Invoke();
        }

        public void ToggleLayoutMode() => SetLayoutMode(!LayoutMode, "hotkey/tray");

        /// <summary>选中某个节点（布局模式与设置面板共用）。</summary>
        public void SelectNode(int id)
        {
            SelectedNodeId = id;
            _window.Renderer.SelectedNode = P.NodeById(id);
            _window.Renderer.Invalidate();
            LayoutStateChanged?.Invoke();
        }

        /// <summary>
        /// 当前选中的节点。未选中（或选中的节点已被删掉）时回落到第一个节点 ——
        /// 否则设置面板会一边显示「#1」一边说「没选中」。
        /// </summary>
        public KvFmNode SelectedNode
        {
            get
            {
                var n = P.NodeById(SelectedNodeId);
                if (n != null) return n;
                return (P.CustomNodes != null && P.CustomNodes.Count > 0) ? P.CustomNodes[0] : null;
            }
        }

        /// <summary>把参考坐标下的点映射到最上面的可见节点（无命中返回 null）。</summary>
        public KvFmNode HitTestNode(double refX, double refY)
        {
            var nodes = P.VisibleNodes();
            for (int i = nodes.Count - 1; i >= 0; i--)   // 后画的在上方，优先命中
            {
                var n = nodes[i];
                if (n.Unselectable) continue;
                if (refX >= n.X && refX <= n.X + n.Width &&
                    refY >= n.Y && refY <= n.Y + n.Height) return n;
            }
            return null;
        }

        /// <summary>
        /// 移动节点（参考坐标，钳制在画布内），并同步已生成的键槽 ——
        /// 拖动时只改这一处，不做整表重建，避免雨线被整批清掉。
        /// </summary>
        public void MoveNodeTo(KvFmNode n, double x, double y)
        {
            if (n == null) return;

            double maxX = Math.Max(0, CanvasWidthRef - n.Width);
            double maxY = Math.Max(0, KvGeometry.CanvasHeight - n.Height);

            n.X = (float)Math.Round(Math.Clamp(x, 0, maxX));
            n.Y = (float)Math.Round(Math.Clamp(y, 0, maxY));

            SyncSlot(n);

            _window.Renderer.SelectedNode = n;
            _window.Renderer.Invalidate();
        }

        /// <summary>把节点的 X/Y 同步进已有的键槽（无则忽略）。</summary>
        private void SyncSlot(KvFmNode n)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!ReferenceEquals(_slots[i].Node, n)) continue;
                var s = _slots[i];
                s.X = n.X;
                s.CenterY = KvGeometry.CanvasHeight - n.Y - n.Height * 0.5f;
                _slots[i] = s;
            }
        }

        /// <summary>鼠标位置（物理像素）→ 画布参考坐标。</summary>
        private bool TryCursorRef(out double refX, out double refY)
        {
            refX = refY = 0;
            if (!_window.IsVisible) return false;
            if (!Win32.GetCursorPosition(out int px, out int py)) return false;

            double dpi = _window.DpiScale;
            double scale = _window.Renderer.Scale;
            if (!(scale > 0.0001)) return false;

            refX = (px / dpi - _window.Left) / scale;
            refY = (py / dpi - _window.Top) / scale;
            return true;
        }

        private void HitTick()
        {
            if (!LayoutMode) return;

            // Esc 退出（按键轮询而非键盘消息 —— 覆盖层是 WS_EX_NOACTIVATE，收不到键盘）
            bool esc = Win32.IsKeyDown(Win32.VK_ESCAPE);
            if (esc && !_escWasDown)
            {
                _escWasDown = true;
                Diag.Log("layout esc -> exit");
                SetLayoutMode(false, "esc");
                return;
            }
            _escWasDown = esc;

            HandleMouseDrag();

            // 方向键微调默认关闭：方向键经常被游戏 / 浏览器占用，
            // 开着的话在别的窗口按方向键会把选中节点一路带偏。
            if (Store.Settings.ArrowNudge) HandleNudge();

            if (_dragNode != null) return;   // 拖动中保持可交互

            bool over = false;
            if (TryCursorRef(out double rx, out double ry))
                over = HitTestNode(rx, ry) != null;

            if (over && _window.ClickThrough)
            {
                _window.ClickThrough = false;
                Diag.Log(string.Format("hit node -> interactive at ref=({0:0.#},{1:0.#})", rx, ry));
            }
            else if (!over && !_window.ClickThrough)
            {
                _window.ClickThrough = true;
                Diag.Log("miss -> click-through");
            }
        }

        /// <summary>
        /// 左键拖动。用 <c>GetAsyncKeyState(VK_LBUTTON)</c> 轮询而不是 WPF 的鼠标事件 ——
        /// 覆盖层是分层 + 不可激活窗口，实测 WPF 的 MouseDown/MouseMove 根本投递不到，
        /// 而轮询在窗口非激活、无焦点时照样准确。
        /// </summary>
        private void HandleMouseDrag()
        {
            bool down = Win32.IsKeyDown(Win32.VK_LBUTTON);

            if (!TryCursorRef(out double rx, out double ry))
            {
                _lbtnWasDown = down;
                return;
            }

            if (down && !_lbtnWasDown)
            {
                // 上升沿：命中哪个节点就抓哪个
                var n = HitTestNode(rx, ry);
                if (n != null)
                {
                    _dragNode = n;
                    _dragOffX = rx - n.X;
                    _dragOffY = ry - n.Y;
                    _dragged = false;
                    SelectNode(n.Id);
                }
            }
            else if (down && _dragNode != null)
            {
                MoveNodeTo(_dragNode, rx - _dragOffX, ry - _dragOffY);
                _dragged = true;
            }
            else if (!down && _lbtnWasDown && _dragNode != null)
            {
                var n = _dragNode;
                _dragNode = null;

                if (_dragged)
                {
                    _dragged = false;
                    FlushStats();
                    Store.SaveProfile();
                    Diag.Log(string.Format("node #{0} moved -> X={1:0} Y={2:0} (saved)", n.Id, n.X, n.Y));
                    LayoutStateChanged?.Invoke();
                }
            }

            _lbtnWasDown = down;
        }

        /// <summary>方向键微调选中节点（Shift 为 10 倍），带 350ms 延迟后 40ms 的连发。</summary>
        private void HandleNudge()
        {
            var n = SelectedNode;
            if (n == null) return;

            float step = Win32.IsKeyDown(Win32.VK_SHIFT) ? 10f : 1f;
            double now = Now();
            bool moved = false;

            int[] vks = { Win32.VK_LEFT, Win32.VK_UP, Win32.VK_RIGHT, Win32.VK_DOWN };

            for (int i = 0; i < vks.Length; i++)
            {
                bool down = Win32.IsKeyDown(vks[i]);
                bool fire = false;

                if (down && !_nudgeWas[i]) { fire = true; _nudgeNext[i] = now + 0.35; }
                else if (down && now >= _nudgeNext[i]) { fire = true; _nudgeNext[i] = now + 0.04; }

                _nudgeWas[i] = down;
                if (!fire) continue;

                double dx = i == 0 ? -step : (i == 2 ? step : 0);
                double dy = i == 1 ? -step : (i == 3 ? step : 0);
                MoveNodeTo(n, n.X + dx, n.Y + dy);
                moved = true;
            }

            if (moved) QueueSave();
        }

        // ---------------------------------------------------------------
        // 运行时控制（供托盘 / 热键 / 设置面板调用）
        // ---------------------------------------------------------------

        /// <summary>覆盖层当前是否可见。</summary>
        public bool Visible => _window.IsVisible;

        /// <summary>当前覆盖层的 DPI 缩放（DIP → 物理像素）。</summary>
        public double DpiScale => _window.DpiScale;

        public void Show()
        {
            _window.Show();
            Win32.BringToTop(_window.Handle);
            _window.Renderer.Invalidate();
        }

        public void Hide()
        {
            FlushStats();
            _window.Hide();
        }

        public void ToggleVisible()
        {
            if (Visible) Hide();
            else Show();
        }

        /// <summary>当前档案被外部改动后，重新解析并重排一次（不读盘）。</summary>
        public void ReloadFromProfile()
        {
            SavePendingStats();
            ApplyProfile();
            Rain.ClearAll(_keys.Values);
            _window.Renderer.Invalidate();
            Changed?.Invoke();
        }

        /// <summary>切换档案并立即生效。</summary>
        public bool SwitchProfile(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            FlushStats();
            if (!Store.SwitchProfile(name)) return false;
            ApplyProfile();
            Rain.ClearAll(_keys.Values);
            _window.Renderer.Invalidate();
            Changed?.Invoke();
            return true;
        }

        /// <summary>切换到列表中的下一个档案（热键用）。</summary>
        public void SwitchNextProfile()
        {
            var names = Store.ScanProfiles();
            if (names == null || names.Count == 0) return;

            int cur = names.FindIndex(n =>
                string.Equals(n, Store.CurrentProfile, StringComparison.OrdinalIgnoreCase));
            int next = (cur + 1) % names.Count;
            SwitchProfile(names[next]);
        }

        /// <summary>保存当前帧的计数到内存（不落盘）。</summary>
        private void SavePendingStats()
        {
            if (P == null) return;

            // 自由布局的计数存在节点自己的 Count 上，不能写进预设的 Count[]
            if (KvGeometry.IsCustom(P.StyleEnum))
            {
                foreach (var slot in _slots)
                {
                    var n = slot.Node;
                    if (n == null) continue;
                    if (!_keys.TryGetValue(slot.Index, out var k)) continue;
                    n.Count = (int)Math.Min(int.MaxValue, k.Count);
                }
            }
            else
            {
                var counts = P.Count;
                if (counts != null && counts.Length == 40)
                {
                    foreach (var kv in _keys)
                    {
                        int idx = kv.Key;
                        if (idx < 0 || idx >= counts.Length) continue;
                        counts[idx] = (int)Math.Min(int.MaxValue, kv.Value.Count);
                    }
                }
            }

            P.TotalCount = _totalCount;
        }

        /// <summary>档案或设置发生变化时触发，供设置面板刷新界面。</summary>
        public event Action Changed;
    }
}

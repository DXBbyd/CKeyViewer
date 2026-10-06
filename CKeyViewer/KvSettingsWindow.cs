using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CKeyViewer.Core;
using CKeyViewer.Render;
using CKeyViewer.Ui;

namespace CKeyViewer
{
    /// <summary>
    /// 设置面板。所有改动即时生效（直接写进 <see cref="KvProfile"/> 并重载渲染），
    /// 同时又以 600ms 防抖落盘，既跟手也不伤磁盘。
    /// </summary>
    public sealed class KvSettingsWindow : Window
    {
        private readonly KvHost _host;
        private readonly ListBox _nav;
        private readonly ContentControl _body;
        private readonly TextBlock _title;
        private readonly TextBlock _status;
        private readonly ScrollViewer _scroll;

        private int _tab;
        private bool _suppress;
        private bool _rebuildQueued;

        /// <summary>「自由布局」标签页的下标（拖动模式与主机状态联动时用到）。</summary>
        private const int CustomTab = 2;

        private static readonly string[] Tabs =
        {
            "档案", "布局", "自由布局", "外观", "文字", "雨线", "按键绑定", "每键配色", "按压动画", "统计", "热键信息"
        };

        private KvProfile P => _host.P;

        public KvSettingsWindow(KvHost host)
        {
            _host = host;

            Title = "CKeyViewer 设置";

            // 尺寸要按「工作区的 DIP 尺寸」来夹取 —— 在 1.25 倍缩放的 1366×768 屏上，
            // 工作区只有 ~1093×566 DIP，写死 940×660 会被挤出屏幕（Top 变负数）。
            var wa = SystemParameters.WorkArea;
            Width = Math.Max(720, Math.Min(940, wa.Width - 40));
            Height = Math.Max(430, Math.Min(660, wa.Height - 40));
            MinWidth = Math.Min(780, Width);
            MinHeight = Math.Min(430, Height);
            MaxWidth = wa.Width;
            MaxHeight = wa.Height;

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowActivated = true;
            Background = Kit.Bg;
            Foreground = Kit.Text;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            SnapsToDevicePixels = true;

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(168) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ---- 标题栏 ----
            var header = new Border
            {
                Background = Kit.Sidebar,
                Padding = new Thickness(16, 10, 12, 10)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            _title = Kit.Text2("CKeyViewer", 15, Kit.Text, bold: true);
            titleStack.Children.Add(_title);
            _status = Kit.Text2("", 11.5, Kit.Sub);
            titleStack.Children.Add(_status);
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            var close = Kit.Button("关闭", () => Close());
            Grid.SetColumn(close, 1);
            headerGrid.Children.Add(close);

            header.Child = headerGrid;
            Grid.SetColumnSpan(header, 2);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ---- 左侧导航 ----
            _nav = new ListBox
            {
                Background = Kit.Sidebar,
                BorderThickness = new Thickness(0),
                Foreground = Kit.Text,
                FontSize = 13,
                Padding = new Thickness(6, 8, 6, 8)
            };
            foreach (var t in Tabs) _nav.Items.Add(t);

            // 记住上次打开的标签页（与 jipper settings.json 的 UiTab 是同一个字段）
            _tab = Math.Max(0, Math.Min(Tabs.Length - 1, _host.Store.Settings.UiTab));
            _nav.SelectedIndex = _tab;

            _nav.SelectionChanged += (s, e) =>
            {
                if (_nav.SelectedIndex < 0) return;

                _tab = _nav.SelectedIndex;
                _host.Store.Settings.UiTab = _tab;
                _host.QueueSave();
                Rebuild();
            };
            Grid.SetColumn(_nav, 0);
            Grid.SetRow(_nav, 1);
            root.Children.Add(_nav);

            // ---- 右侧内容 ----
            _body = new ContentControl();
            _scroll = new ScrollViewer
            {
                Content = _body,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(18, 6, 18, 18),
                Background = Kit.Bg
            };
            Grid.SetColumn(_scroll, 1);
            Grid.SetRow(_scroll, 1);
            root.Children.Add(_scroll);

            Content = root;
            Resources = Kit.Theme();

            _host.Changed += OnHostChanged;
            _host.LayoutStateChanged += OnLayoutStateChanged;
            Closed += (s, e) =>
            {
                _restoreOnExit = false;
                // 关窗口时先退出布局模式，免得覆盖层一直不穿透
                _host.SetLayoutMode(false, "settings closed");
                _host.Changed -= OnHostChanged;
                _host.LayoutStateChanged -= OnLayoutStateChanged;
                _host.FlushNow();
            };

            Rebuild();
            UpdateHeader();
        }

        /// <summary>
        /// 进出布局模式时把设置面板让开 —— 它正好盖在屏幕中央，而节点多半也在中间。
        /// 退出时再恢复，用户不用自己去点最小化 / 还原。
        /// </summary>
        private void OnLayoutStateChanged()
        {
            try
            {
                if (_host.LayoutMode)
                {
                    if (IsVisible && WindowState == WindowState.Normal)
                    {
                        _restoreOnExit = true;
                        WindowState = WindowState.Minimized;
                    }
                }
                else if (_restoreOnExit)
                {
                    _restoreOnExit = false;
                    WindowState = WindowState.Normal;
                    Activate();
                }
            }
            catch (Exception ex) { Diag.Log("layout minimize: " + ex.Message); }

            OnHostChanged();
        }

        private bool _restoreOnExit;
        private void OnHostChanged()
        {
            if (_suppress) return;
            UpdateHeader();

            // 异步重建：这些通知可能来自控件自身的事件回调（比如节点下拉的点击），
            // 当场把控件树换掉会把正在派发的事件弄崩。
            if (_rebuildQueued) return;
            _rebuildQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _rebuildQueued = false;
                if (!IsLoaded && IsVisible == false) return;
                Rebuild(keepScroll: true);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void UpdateHeader()
        {
            _title.Text = "CKeyViewer  ——  档案：" + _host.Store.CurrentProfile;
            _status.Text = string.Format("配置目录：{0}    |    共 {1} 个档案    |    累计按键 {2:N0}",
                _host.Store.Root, _host.Store.ScanProfiles().Count, _host.TotalCount);
        }

        // ---------------------------------------------------------------
        // 改动入口
        // ---------------------------------------------------------------

        /// <summary>执行一次改动：即时生效 + 延迟落盘；<paramref name="rebuild"/> 用于结构性改动。</summary>
        private void Apply(Action mutate, bool rebuild = false)
        {
            _suppress = true;
            try
            {
                mutate();
                _host.ReloadFromProfile();
                _host.QueueSave();
            }
            catch (Exception ex)
            {
                Diag.Log("settings apply: " + ex);
            }
            finally
            {
                _suppress = false;
            }

            if (rebuild) Rebuild();
            UpdateHeader();
        }

        // ---------------------------------------------------------------
        // 页面构建
        // ---------------------------------------------------------------

        private void Rebuild() => Rebuild(false);

        private void Rebuild(bool keepScroll)
        {
            // 结构性变化（档案切换 / 样式切换）导致 tab 失效时回落
            if (_tab < 0 || _tab >= Tabs.Length) _tab = 0;

            double offset = keepScroll ? _scroll.VerticalOffset : 0;
            var panel = new StackPanel();

            try
            {
                switch (_tab)
                {
                    case 0: BuildProfile(panel); break;
                    case 1: BuildLayout(panel); break;
                    case 2: BuildCustom(panel); break;
                    case 3: BuildAppearance(panel); break;
                    case 4: BuildText(panel); break;
                    case 5: BuildRain(panel); break;
                    case 6: BuildKeys(panel); break;
                    case 7: BuildPerKey(panel); break;
                    case 8: BuildAnimation(panel); break;
                    case 9: BuildStats(panel); break;
                    default: BuildInfo(panel); break;
                }
            }
            catch (Exception ex)
            {
                Diag.Log("settings build: " + ex);
                panel.Children.Add(Kit.Text2("构建此页面时出错：" + ex.Message, 12, Kit.Sub));
            }

            _body.Content = panel;

            if (keepScroll && offset > 0)
            {
                _scroll.UpdateLayout();
                _scroll.ScrollToVerticalOffset(offset);
            }
            else
            {
                _scroll.ScrollToTop();
            }
        }

        // ---- 0. 档案 ----

        private void BuildProfile(Panel p)
        {
            p.Children.Add(Kit.Section("当前档案"));
            p.Children.Add(Kit.Hint("档案对应 jipper 的 config/profiles/*.json，可以直接互相复制使用。"));

            var names = _host.Store.ScanProfiles();
            if (names.Count == 0) names.Add(_host.Store.CurrentProfile);

            int idx = names.FindIndex(n =>
                string.Equals(n, _host.Store.CurrentProfile, StringComparison.OrdinalIgnoreCase));

            p.Children.Add(Kit.Combo("选择档案", names, () => Math.Max(0, idx), i =>
            {
                if (i < 0 || i >= names.Count) return;
                Apply(() => _host.Store.SwitchProfile(names[i]), rebuild: true);
            }, 260));

            p.Children.Add(Kit.HRow(
                Kit.Button("新建…", () =>
                {
                    string name = InputDialog.Ask(this, "新建档案", "请输入档案名称：", "Profile");
                    if (string.IsNullOrWhiteSpace(name)) return;
                    Apply(() => _host.Store.CreateProfile(name, copyCurrent: true), rebuild: true);
                }, accent: true),
                Kit.Button("重命名…", () =>
                {
                    string nn = InputDialog.Ask(this, "重命名档案", "新的名称：", _host.Store.CurrentProfile);
                    if (string.IsNullOrWhiteSpace(nn)) return;
                    Apply(() => _host.Store.RenameProfile(_host.Store.CurrentProfile, nn), rebuild: true);
                }),
                Kit.DangerButton("删除", () =>
                {
                    if (_host.Store.ScanProfiles().Count <= 1)
                    {
                        MessageBox.Show(this, "至少需要保留一个档案。", "CKeyViewer",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    if (MessageBox.Show(this, "确定删除档案「" + _host.Store.CurrentProfile + "」？此操作不可撤销。",
                            "CKeyViewer", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                        return;
                    Apply(() => _host.Store.DeleteProfile(_host.Store.CurrentProfile), rebuild: true);
                })
            ));

            p.Children.Add(Kit.Section("通用开关"));
            p.Children.Add(Kit.Check("启用覆盖层（关闭后完全隐藏）", () => P.Enabled, v =>
            {
                Apply(() =>
                {
                    P.Enabled = v;
                    if (v) _host.Show(); else _host.Hide();
                });
            }));
            p.Children.Add(Kit.Check("流媒体模式（隐藏 KPS / Total 数字）", () => P.StreamerMode,
                v => Apply(() => P.StreamerMode = v)));

            p.Children.Add(Kit.Section("维护"));
            p.Children.Add(Kit.HRow(
                Kit.Button("重置计数", () =>
                {
                    if (MessageBox.Show(this, "把所有按键计数归零？", "CKeyViewer",
                            MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                    {
                        _host.ResetCounts();
                        UpdateHeader();
                    }
                }),
                Kit.Button("打开配置目录", () =>
                {
                    try
                    {
                        Directory.CreateDirectory(_host.Store.Root);
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = _host.Store.Root,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex) { Diag.Log(ex.Message); }
                }),
                Kit.Button("立即保存", () => { _host.FlushNow(); UpdateHeader(); })
            ));
        }

        // ---- 1. 布局 ----

        private void BuildLayout(Panel p)
        {
            string[] styles = { "Key12", "Key16 (默认)", "Key20", "Key10", "Key8", "Key14", "Key24", "Full108 全键盘", "自定义布局" };
            string[] footStyles = { "无", "脚键 2", "脚键 4", "脚键 6", "脚键 8", "脚键 10", "脚键 12", "脚键 14", "脚键 16" };

            p.Children.Add(Kit.Section("主键布局"));
            p.Children.Add(Kit.Combo("布局样式", styles, () => P.KeyViewerStyle,
                i => Apply(() => P.KeyViewerStyle = i, rebuild: true), 260));

            bool full = KvGeometry.IsFullKeyboard(P.StyleEnum);
            if (!full)
            {
                p.Children.Add(Kit.Combo("脚键布局", footStyles, () => P.FootKeyViewerStyle,
                    i => Apply(() => P.FootKeyViewerStyle = i, rebuild: true), 260));
            }

            p.Children.Add(Kit.Hint("Full108 会显示 105 个键位，不显示脚键与雨线（与原版一致）。"));

            p.Children.Add(Kit.Section("尺寸与位置"));
            p.Children.Add(Kit.Slider("整体尺寸 Size", 0.2, 4.0, () => P.Size, v => Apply(() => P.Size = (float)v), "0.00"));
            p.Children.Add(Kit.Check("标准键宽（Key10/12/20 的窄键版）", () => P.StandardKeyWidth,
                v => Apply(() => P.StandardKeyWidth = v, rebuild: true)));
            p.Children.Add(Kit.Check("整体下移 200 单位（DownLocation）", () => P.DownLocation,
                v => Apply(() => P.DownLocation = v, rebuild: true)));

            p.Children.Add(Kit.Check("启用自定义位置（否则自动贴工作区底边居中）", () => P.CustomPositionEnabled,
                v => Apply(() => P.CustomPositionEnabled = v, rebuild: true)));

            if (P.CustomPositionEnabled)
            {
                p.Children.Add(Kit.Slider("主键 X（0=左 1=右）", 0, 1, () => P.MainKeyViewerPosition.x,
                    v => Apply(() => P.MainKeyViewerPosition = KvPos.Of((float)v, P.MainKeyViewerPosition.y)), "0.000"));
                p.Children.Add(Kit.Slider("主键 Y（0=顶 1=底）", 0, 1, () => P.MainKeyViewerPosition.y,
                    v => Apply(() => P.MainKeyViewerPosition = KvPos.Of(P.MainKeyViewerPosition.x, (float)v)), "0.000"));
            }

            p.Children.Add(Kit.Section("预览信息"));
            p.Children.Add(Kit.Hint(string.Format(
                "参考画布 1080 单位高；1 单位 ≈ 屏幕高/1080 × Size 像素。\r\n" +
                "当前键位数：{0}（含脚键），布局行数随样式自动匹配。",
                KvGeometry.TotalKeyCount(P.StyleEnum) +
                (P.FootKeyViewerStyle == 0 ? 0 : KvProfile.FootKeyCount(P.FootStyleEnum)))));

            if (KvGeometry.IsCustom(P.StyleEnum))
            {
                p.Children.Add(Kit.Section("自定义布局"));
                p.Children.Add(Kit.Hint("当前是自定义布局，键位来自自定义节点（本页的样式表/尺寸参数不再生效）。"));
                p.Children.Add(Kit.HRow(
                    Kit.Button("打开「自由布局」标签页", () => { _tab = CustomTab; _nav.SelectedIndex = CustomTab; }, accent: true),
                    Kit.Button(_host.LayoutMode ? "退出拖动模式（Esc）" : "▶ 在屏幕上拖动调整",
                        () => _host.SetLayoutMode(!_host.LayoutMode, "settings button"))
                ));
            }
        }

        // ---- 2. 自由布局（FmNode / FmLayerGroup）----

        private void BuildCustom(Panel p)
        {
            bool custom = KvGeometry.IsCustom(P.StyleEnum);

            p.Children.Add(Kit.Section("自由布局"));
            p.Children.Add(Kit.Hint(
                "自由布局下每个元素都是一个「节点」，位置用**画布绝对坐标**：\r\n" +
                "X = 左边缘距画布左边，Y = 上边缘距画布顶边。画布高度恒为 1080，" +
                "宽度 = 1080 × 屏幕宽高比。\r\n" +
                "节点数据就是 jipper 的 CustomNodes / LayerGroups，配置可双向读写。"));

            p.Children.Add(Kit.Check("使用自定义布局（KeyViewerStyle = Custom）", () => custom, v =>
            {
                Apply(() =>
                {
                    if (v) P.KeyViewerStyle = (int)KeyviewerStyle.Custom;
                    else if (P.KeyViewerStyle == (int)KeyviewerStyle.Custom)
                        P.KeyViewerStyle = (int)KeyviewerStyle.Key16;
                }, true);
            }));

            if (!custom)
            {
                p.Children.Add(Kit.Hint(
                    "当前用的是预设布局。勾上上面的开关即可切到自由布局；\r\n" +
                    "想让改动有个起点，可以用下面的按钮把现在的预设布局「拍平」成节点。"));

                var previewStyle = P.StyleEnum;
                bool canFlatten = previewStyle != KeyviewerStyle.Custom &&
                                  !KvGeometry.IsFullKeyboard(previewStyle);
                p.Children.Add(Kit.HRow(
                    Kit.Button("从当前预设布局生成节点…", () =>
                    {
                        if (P.CustomNodes.Count > 0 &&
                            MessageBox.Show(this,
                                "这会覆盖现有的 " + P.CustomNodes.Count + " 个节点，确定继续？",
                                "CKeyViewer", MessageBoxButton.OKCancel,
                                MessageBoxImage.Warning) != MessageBoxResult.OK) return;

                        int first = 0;
                        Apply(() =>
                        {
                            P.RebuildCustomFromPreset(previewStyle);
                            P.KeyViewerStyle = (int)KeyviewerStyle.Custom;
                            if (P.CustomNodes.Count > 0) first = P.CustomNodes[0].Id;
                        }, true);
                        _host.SelectNode(first);
                    }, accent: true),
                    Kit.Hint("（Full108 全键盘不支持拍平）")
                ));
                return;
            }

            // ================= 节点列表 =================
            var nodes = P.CustomNodes;
            p.Children.Add(Kit.Section("节点（" + nodes.Count + "）"));

            p.Children.Add(Kit.HRow(
                Kit.Button("＋ 按键", () => AddNode(0), accent: true),
                Kit.Button("＋ KPS", () => AddNode(1)),
                Kit.Button("＋ Total", () => AddNode(2)),
                Kit.Button("＋ 图片", () => AddNode(3))
            ));

            if (nodes.Count == 0)
            {
                p.Children.Add(Kit.Hint("还没有节点。用上面的按钮新建一个，或从预设布局生成。"));
                return;
            }

            var labels = new List<string>(nodes.Count);
            int curId = _host.SelectedNode != null ? _host.SelectedNode.Id : 0;
            int cur = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                string suffix = n.Hidden ? "  · 隐藏"
                              : string.IsNullOrEmpty(n.GroupId) ? ""
                              : "  · " + GroupName(n.GroupId);
                labels.Add(string.Format("#{0}  {1}{2}{3}",
                    n.Id, n.TypeName,
                    string.IsNullOrEmpty(n.KeyBind) ? "" : "  " + n.KeyBind,
                    suffix));
                if (n.Id == curId) cur = i;
            }

            p.Children.Add(Kit.Combo("选中节点", labels, () => cur, i =>
            {
                if (i >= 0 && i < nodes.Count) _host.SelectNode(nodes[i].Id);
            }, 300));

            var sel = _host.SelectedNode;
            if (sel == null)
            {
                p.Children.Add(Kit.Hint("在上面挑一个节点，或者点「＋」新建一个。"));
                return;
            }

            p.Children.Add(Kit.HRow(
                Kit.Button("复制", () =>
                {
                    int newId = 0;
                    Apply(() => { var c = P.DuplicateNode(sel.Id); if (c != null) newId = c.Id; }, true);
                    if (newId > 0) _host.SelectNode(newId);
                }),
                Kit.Button("置顶", () => Apply(() => P.SetNodeDepth(sel.Id, MaxDepth() + 1), true)),
                Kit.Button("置底", () => Apply(() => P.SetNodeDepth(sel.Id, MinDepth() - 1), true)),
                Kit.DangerButton("删除", () =>
                {
                    if (MessageBox.Show(this, "删除节点 #" + sel.Id + "？此操作不可撤销。", "CKeyViewer",
                            MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

                    int fallback = 0;
                    Apply(() =>
                    {
                        P.RemoveNode(sel.Id);
                        if (P.CustomNodes.Count > 0) fallback = P.CustomNodes[0].Id;
                    }, true);
                    _host.SelectNode(fallback);
                })
            ));

            p.Children.Add(Kit.Hint(string.Format(
                "当前节点：{0} · Depth {1}{2}", sel.TypeName, sel.Depth,
                sel.Unselectable ? " · 不可被鼠标选中" : "")));

            p.Children.Add(Kit.Section("拖动摆放"));
            p.Children.Add(Kit.HRow(
                Kit.Button(_host.LayoutMode ? "退出拖动模式（Esc）" : "▶ 在屏幕上拖动调整",
                    () => _host.SetLayoutMode(!_host.LayoutMode, "settings button"), accent: true)
            ));
            p.Children.Add(Kit.Hint(
                "进入后覆盖层会显示参考网格与节点虚线框：\r\n" +
                "· 鼠标压在节点上时可以直接拖（别处照旧鼠标穿透，不会挡住桌面操作）\r\n" +
                "· 按 Esc 退出并自动保存\r\n" +
                "· 设置面板会自动最小化，免得挡住节点"));
            p.Children.Add(Kit.Check("允许用方向键微调选中节点（Shift 为 10 单位）",
                () => _host.Store.Settings.ArrowNudge,
                v => Apply(() => _host.Store.Settings.ArrowNudge = v, true)));
            p.Children.Add(Kit.Hint(_host.Store.Settings.ArrowNudge
                ? "已开启：方向键每按一次移动 1 单位。注意方向键常被游戏 / 浏览器占用，小心误触。"
                : "默认关闭 —— 方向键常被游戏 / 浏览器占用，开着的话在别的窗口按方向键会把节点悄悄带偏。"));

            p.Children.Add(Kit.Section("位置与尺寸（画布单位）"));
            p.Children.Add(Kit.NumberRow("X（左边缘）", () => sel.X,
                v => Apply(() => sel.X = (float)v), 1, 0, Math.Round(_host.CanvasWidthRef)));
            p.Children.Add(Kit.NumberRow("Y（上边缘）", () => sel.Y,
                v => Apply(() => sel.Y = (float)v), 1, 0, KvGeometry.CanvasHeight));
            p.Children.Add(Kit.NumberRow("宽 Width", () => sel.Width,
                v => Apply(() => sel.Width = (float)Math.Max(2, v)), 1, 2, 4000));
            p.Children.Add(Kit.NumberRow("高 Height", () => sel.Height,
                v => Apply(() => sel.Height = (float)Math.Max(2, v)), 1, 2, 4000));
            p.Children.Add(Kit.NumberRow("层级 Depth（小的先画）", () => sel.Depth,
                v => Apply(() => P.SetNodeDepth(sel.Id, (int)v), true), 1, -100000, 100000));

            p.Children.Add(Kit.Section("通用"));
            p.Children.Add(Kit.Check("隐藏该节点（Hidden）", () => sel.Hidden,
                v => Apply(() => sel.Hidden = v, true)));
            p.Children.Add(Kit.Check("布局模式下不可被鼠标选中（Unselectable）", () => sel.Unselectable,
                v => Apply(() => sel.Unselectable = v)));

            // ================= 按键类节点 =================
            if (sel.NodeType == 0)
            {
                p.Children.Add(Kit.Section("按键绑定"));
                p.Children.Add(Kit.Hint("KeyBind 存的是 Unity KeyCode 的枚举名（A / Space / LeftShift …），与原版一致。"));
                p.Children.Add(Kit.KeyRow("绑定按键", () => KeyCodeMap.FromName(sel.KeyBind),
                    v => Apply(() => sel.KeyBind = KeyCodeMap.NameOf(v))));
                p.Children.Add(Kit.TextBoxRow("键名文字（留空 = 键码名）", () => sel.CustomText,
                    v => Apply(() => sel.CustomText = v ?? "")));
                p.Children.Add(Kit.TextBoxRow("按下时文字（留空 = 不变）", () => sel.PressedText,
                    v => Apply(() => sel.PressedText = v ?? "")));
                p.Children.Add(Kit.KeyRow("鬼键（只触发雨线，不计数）", () => KeyCodeMap.FromName(sel.GhostKey),
                    v => Apply(() => sel.GhostKey = KeyCodeMap.NameOf(v))));

                p.Children.Add(Kit.Section("显示"));
                p.Children.Add(Kit.Check("隐藏按键名（HideLabel）", () => sel.HideLabel,
                    v => Apply(() => sel.HideLabel = v)));
                p.Children.Add(Kit.Check("隐藏计数（HideCount）", () => sel.HideCount,
                    v => Apply(() => sel.HideCount = v)));
                p.Children.Add(Kit.Check("计数计入 Total（CountInTotal）", () => sel.CountInTotal,
                    v => Apply(() => sel.CountInTotal = v)));
                p.Children.Add(Kit.Check("单独统计本键 KPS（PerKeyKps）", () => sel.PerKeyKps,
                    v => Apply(() => sel.PerKeyKps = v)));
                p.Children.Add(Kit.Hint(string.Format("点击次数：{0:N0}（原版存在节点自己的 Count 上）", sel.Count)));
            }
            else
            {
                p.Children.Add(Kit.Section("统计条"));
                p.Children.Add(Kit.Hint(sel.NodeType == 1
                    ? "这是 KPS 统计条节点。文字与配色沿用全局的 KPS 设置（可在「文字」「外观」页调整）。"
                    : "这是 Total 统计条节点。文字与配色沿用全局的 Total 设置（可在「文字」「外观」页调整）。"));
            }

            // ================= 字体 / 形状 =================
            p.Children.Add(Kit.Section("字体与形状（0 = 沿用全局）"));
            p.Children.Add(Kit.NumberRow("字号 FontSize", () => sel.FontSize,
                v => Apply(() => sel.FontSize = (float)v), 1, 0, 300));
            p.Children.Add(Kit.NumberRow("圆角 CornerRadius", () => sel.CornerRadius,
                v => Apply(() => sel.CornerRadius = (float)v), 1, 0, 400));
            p.Children.Add(Kit.NumberRow("边框宽度 BorderThickness", () => sel.BorderThickness,
                v => Apply(() => sel.BorderThickness = (float)v), 0.5, 0, 60, "0.#"));
            p.Children.Add(Kit.Slider("不透明度 Opacity", 0.02, 1,
                () => sel.Opacity <= 0f ? 1.0 : sel.Opacity,
                v => Apply(() => sel.Opacity = (float)v), "0.00"));

            // ================= 节点配色 =================
            p.Children.Add(Kit.Section("节点配色"));
            p.Children.Add(Kit.Check("使用节点自定义配色（UseCustomColor）", () => sel.UseCustomColor, v =>
            {
                Apply(() =>
                {
                    if (v) InitNodeColors(sel);
                    sel.UseCustomColor = v;
                }, true);
            }));

            if (sel.UseCustomColor)
            {
                p.Children.Add(Kit.ColorRow("背景色", () => KvHost.NodeColor(sel.Bg, P.Background),
                    v => Apply(() => sel.Bg = KvHost.ToNodeColor(v))));
                p.Children.Add(Kit.ColorRow("按下背景色", () => KvHost.NodeColor(sel.BgPressed, P.BackgroundClicked),
                    v => Apply(() => sel.BgPressed = KvHost.ToNodeColor(v))));
                p.Children.Add(Kit.ColorRow("描边色", () => KvHost.NodeColor(sel.Outline, P.Outline),
                    v => Apply(() => sel.Outline = KvHost.ToNodeColor(v))));
                p.Children.Add(Kit.ColorRow("按下描边色", () => KvHost.NodeColor(sel.OutlinePressed, P.OutlineClicked),
                    v => Apply(() => sel.OutlinePressed = KvHost.ToNodeColor(v))));
                p.Children.Add(Kit.ColorRow("文字色", () => KvHost.NodeColor(sel.TextColor, P.Text),
                    v => Apply(() => sel.TextColor = KvHost.ToNodeColor(v))));
                p.Children.Add(Kit.ColorRow("按下文字色", () => KvHost.NodeColor(sel.TextColorPressed, P.TextClicked),
                    v => Apply(() => sel.TextColorPressed = KvHost.ToNodeColor(v))));
                p.Children.Add(Kit.Hint("颜色数组是 [r,g,b,a]（0~1），长度必须是 4，否则原版会回落到全局配色。"));
            }

            // ================= 雨线 =================
            if (sel.NodeType == 0)
            {
                p.Children.Add(Kit.Section("节点雨线"));
                p.Children.Add(Kit.Check("该节点启用雨线（RainEnabled）", () => sel.RainEnabled,
                    v => Apply(() => sel.RainEnabled = v, true)));
                if (sel.RainEnabled)
                {
                    p.Children.Add(Kit.Combo("雨线行", new[] { "1 行（默认）", "2 行", "3 行" },
                        () => Math.Max(0, Math.Min(2, sel.RainRow)),
                        i => Apply(() => sel.RainRow = i, true), 160));
                }
            }

            // ================= 图片 / 视频 =================
            if (sel.NodeType == 3)
            {
                p.Children.Add(Kit.Section("图片 / 视频"));
                p.Children.Add(Kit.TextBoxRow("图片路径 ImagePath", () => sel.ImagePath,
                    v => Apply(() => { sel.ImagePath = v ?? ""; OverlayRenderer.ClearImageCache(); }, true), 320));
                p.Children.Add(Kit.TextBoxRow("按下时图片 ImagePathPressed", () => sel.ImagePathPressed,
                    v => Apply(() => { sel.ImagePathPressed = v ?? ""; OverlayRenderer.ClearImageCache(); }, true), 320));
                p.Children.Add(Kit.TextBoxRow("视频路径 VideoPath（仅记录，不播放）", () => sel.VideoPath,
                    v => Apply(() => sel.VideoPath = v ?? ""), 320));
                p.Children.Add(Kit.Check("视频循环 VideoLoop", () => sel.VideoLoop,
                    v => Apply(() => sel.VideoLoop = v)));
                p.Children.Add(Kit.Hint("填绝对路径，或相对于程序目录的路径。图片会缓存，改路径后会自动重读。"));
            }

            // ================= 图层组 =================
            p.Children.Add(Kit.Section("所属图层组"));
            var groupItems = new List<string> { "（不分组）" };
            foreach (var g in P.LayerGroups) groupItems.Add(g.Name + "   [" + g.Id + "]");

            p.Children.Add(Kit.Combo("图层组", groupItems, () =>
            {
                if (string.IsNullOrEmpty(sel.GroupId)) return 0;
                for (int k = 0; k < P.LayerGroups.Count; k++)
                    if (P.LayerGroups[k].Id == sel.GroupId) return k + 1;
                return 0;
            }, i => Apply(() =>
            {
                sel.GroupId = (i <= 0 || i > P.LayerGroups.Count) ? "" : P.LayerGroups[i - 1].Id;
            }), 240));

            p.Children.Add(Kit.Section("图层组管理（" + P.LayerGroups.Count + "）"));
            p.Children.Add(Kit.Hint("组的 Visible 关掉后，组内所有节点一起隐藏 —— 与原版 CustomNodeVisible 的判定一致。"));
            p.Children.Add(Kit.HRow(
                Kit.Button("＋ 新建图层组", () =>
                {
                    string name = InputDialog.Ask(this, "新建图层组", "图层组名称：",
                        "图层组 " + (P.LayerGroups.Count + 1));
                    if (string.IsNullOrWhiteSpace(name)) return;
                    Apply(() => P.NewGroup(name), true);
                }, accent: true)
            ));

            if (P.LayerGroups.Count == 0)
            {
                p.Children.Add(Kit.Hint("还没有图层组。"));
            }
            else
            {
                for (int i = 0; i < P.LayerGroups.Count; i++)
                {
                    var g = P.LayerGroups[i];
                    if (g == null) continue;

                    p.Children.Add(Kit.HRow(
                        Kit.Check(g.Id + "  可见", () => g.Visible, v => Apply(() => g.Visible = v, true)),
                        Kit.TextBoxRow("名称", () => g.Name, v => Apply(() => g.Name = v ?? "", true), 150, 44),
                        Kit.DangerButton("删除组", () =>
                        {
                            if (MessageBox.Show(this, "删除图层组「" + g.Name + "」？组内节点会变成不分组。",
                                    "CKeyViewer", MessageBoxButton.OKCancel,
                                    MessageBoxImage.Warning) != MessageBoxResult.OK) return;
                            Apply(() => P.RemoveGroup(g.Id), true);
                        })
                    ));
                }
            }

            p.Children.Add(Kit.Section("批量操作"));
            p.Children.Add(Kit.HRow(
                Kit.Button("把节点计数归零", () =>
                {
                    if (MessageBox.Show(this, "把自定义节点的点击次数归零？", "CKeyViewer",
                            MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                    Apply(() =>
                    {
                        foreach (var n in P.CustomNodes) if (n != null) n.Count = 0;
                        P.TotalCount = 0;
                    }, true);
                    _host.ResetCounts();
                }),
                Kit.Button("全部显示（清掉隐藏与分组）", () =>
                {
                    Apply(() =>
                    {
                        foreach (var n in P.CustomNodes)
                        {
                            if (n == null) continue;
                            n.Hidden = false;
                            n.GroupId = "";
                        }
                        foreach (var g in P.LayerGroups) if (g != null) g.Visible = true;
                    }, true);
                }),
                Kit.Button("清空所有节点", () =>
                {
                    if (MessageBox.Show(this, "删除全部 " + P.CustomNodes.Count + " 个节点？此操作不可撤销。",
                            "CKeyViewer", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                        return;
                    Apply(() =>
                    {
                        P.CustomNodes.Clear();
                        P.CustomNodeNextId = 1;
                    }, true);
                    _host.SelectNode(0);
                })
            ));
        }

        /// <summary>新建节点后直接选中它，并顺手进入拖动模式 —— 新建的目的就是摆放。</summary>
        private void AddNode(int type)
        {
            int newId = 0;
            Apply(() =>
            {
                float w = (type == 1 || type == 2) ? 220f : 60f;
                float h = (type == 1 || type == 2) ? 36f : 60f;
                var n = P.NewNode(type, 200f, 200f, w, h);
                if (type == 0) n.KeyBind = "A";
                n.UseCustomColor = false;
                newId = n.Id;
            }, true);

            if (newId <= 0) return;
            _host.SelectNode(newId);
            if (!_host.LayoutMode) _host.SetLayoutMode(true, "new node");
        }

        /// <summary>把节点的 6 个颜色数组初始化成当前全局配色，避免一开开关就变黑。</summary>
        private void InitNodeColors(KvFmNode n)
        {
            n.Bg = KvHost.ToNodeColor(P.Background);
            n.BgPressed = KvHost.ToNodeColor(P.BackgroundClicked);
            n.Outline = KvHost.ToNodeColor(P.Outline);
            n.OutlinePressed = KvHost.ToNodeColor(P.OutlineClicked);
            n.TextColor = KvHost.ToNodeColor(P.Text);
            n.TextColorPressed = KvHost.ToNodeColor(P.TextClicked);
        }

        private string GroupName(string id)
        {
            var g = P.GroupFor(id);
            return g == null ? id : g.Name;
        }

        private int MaxDepth()
        {
            int max = 0;
            foreach (var n in P.CustomNodes) if (n != null && n.Depth > max) max = n.Depth;
            return max;
        }

        private int MinDepth()
        {
            int min = 0;
            foreach (var n in P.CustomNodes) if (n != null && n.Depth < min) min = n.Depth;
            return min;
        }

        // ---- 3. 外观 ----

        private void BuildAppearance(Panel p)
        {
            p.Children.Add(Kit.Section("字体"));
            p.Children.Add(Kit.EditableCombo("字体名（可手填）", FontChoices(), P.FontName,
                v => Apply(() => P.FontName = v), 260));
            p.Children.Add(Kit.Slider("键名字号", 6, 90, () => P.KeyFontSize, v => Apply(() => P.KeyFontSize = (float)v)));
            p.Children.Add(Kit.HRow(
                Kit.Check("粗体", () => (P.FontStyleFlags & 1) != 0, v => Apply(() =>
                {
                    P.FontStyleFlags = v ? (P.FontStyleFlags | 1) : (P.FontStyleFlags & ~1);
                })),
                Kit.Check("斜体", () => (P.FontStyleFlags & 2) != 0, v => Apply(() =>
                {
                    P.FontStyleFlags = v ? (P.FontStyleFlags | 2) : (P.FontStyleFlags & ~2);
                }))
            ));
            p.Children.Add(Kit.Hint("字体文件会从程序目录向上回溯，在 assets / jipper/assets / Fonts / CustomFont 里查找。"));

            p.Children.Add(Kit.Section("键帽配色"));
            p.Children.Add(Kit.ColorRow("背景色", () => P.Background, v => Apply(() => P.Background = v)));
            p.Children.Add(Kit.ColorRow("按下背景色", () => P.BackgroundClicked, v => Apply(() => P.BackgroundClicked = v)));
            p.Children.Add(Kit.ColorRow("描边色", () => P.Outline, v => Apply(() => P.Outline = v)));
            p.Children.Add(Kit.ColorRow("按下描边色", () => P.OutlineClicked, v => Apply(() => P.OutlineClicked = v)));
            p.Children.Add(Kit.ColorRow("文字色", () => P.Text, v => Apply(() => P.Text = v)));
            p.Children.Add(Kit.ColorRow("按下文字色", () => P.TextClicked, v => Apply(() => P.TextClicked = v)));

            p.Children.Add(Kit.Section("形状"));
            p.Children.Add(Kit.Hint(string.Format(
                "描边宽度 {0:0.#}、圆角半径 {1:0.#} —— 这两个值在原版里由精灵图与程序化网格决定，\r\n" +
                "没有对应的配置项，这里保持与原版一致的固定值。",
                KvGeometry.OutlineWidth, KvGeometry.CornerRadius)));
        }

        // ---- 4. 文字 ----

        private void BuildText(Panel p)
        {
            p.Children.Add(Kit.Section("统计标签"));
            p.Children.Add(Kit.TextBoxRow("KPS 标签", () => P.KpsLabel, v => Apply(() => P.KpsLabel = v)));
            p.Children.Add(Kit.TextBoxRow("Total 标签", () => P.TotalLabel, v => Apply(() => P.TotalLabel = v)));
            p.Children.Add(Kit.Check("隐藏 KPS / Total 的标签文字（只显示数字）", () => P.HideKpsTotalLabel,
                v => Apply(() => P.HideKpsTotalLabel = v)));
            p.Children.Add(Kit.Check("计数使用千分位", () => P.EnableCountFormatting,
                v => Apply(() => P.EnableCountFormatting = v)));
            p.Children.Add(Kit.Check("隐藏主键下方的点击次数", () => P.HideMainKeyCount,
                v => Apply(() => P.HideMainKeyCount = v)));
            p.Children.Add(Kit.Check("每个键单独统计 KPS", () => P.EnablePerKeyKps,
                v => Apply(() => P.EnablePerKeyKps = v)));

            p.Children.Add(Kit.Section("键名文字效果"));
            p.Children.Add(Kit.Check("启用描边", () => P.EnableKeyTextOutline, v => Apply(() => P.EnableKeyTextOutline = v)));
            p.Children.Add(Kit.ColorRow("描边颜色", () => P.KeyTextOutlineColor, v => Apply(() => P.KeyTextOutlineColor = v)));
            p.Children.Add(Kit.Slider("描边粗细", 0, 2, () => P.KeyTextOutlineThickness, v => Apply(() => P.KeyTextOutlineThickness = (float)v), "0.00"));
            p.Children.Add(Kit.Check("启用阴影", () => P.EnableKeyTextShadow, v => Apply(() => P.EnableKeyTextShadow = v)));
            p.Children.Add(Kit.ColorRow("阴影颜色", () => P.KeyTextShadowColor, v => Apply(() => P.KeyTextShadowColor = v)));
            p.Children.Add(Kit.Slider("阴影 X 偏移", -8, 8, () => P.KeyTextShadowOffsetX, v => Apply(() => P.KeyTextShadowOffsetX = (float)v), "0.0"));
            p.Children.Add(Kit.Slider("阴影 Y 偏移", -8, 8, () => P.KeyTextShadowOffsetY, v => Apply(() => P.KeyTextShadowOffsetY = (float)v), "0.0"));

            p.Children.Add(Kit.Section("计数文字效果"));
            p.Children.Add(Kit.Check("启用描边", () => P.EnableCountTextOutline, v => Apply(() => P.EnableCountTextOutline = v)));
            p.Children.Add(Kit.ColorRow("描边颜色", () => P.CountTextOutlineColor, v => Apply(() => P.CountTextOutlineColor = v)));
            p.Children.Add(Kit.Slider("描边粗细", 0, 2, () => P.CountTextOutlineThickness, v => Apply(() => P.CountTextOutlineThickness = (float)v), "0.00"));
            p.Children.Add(Kit.Check("启用阴影", () => P.EnableCountTextShadow, v => Apply(() => P.EnableCountTextShadow = v)));
            p.Children.Add(Kit.ColorRow("阴影颜色", () => P.CountTextShadowColor, v => Apply(() => P.CountTextShadowColor = v)));
            p.Children.Add(Kit.Slider("阴影 X 偏移", -8, 8, () => P.CountTextShadowOffsetX, v => Apply(() => P.CountTextShadowOffsetX = (float)v), "0.0"));
            p.Children.Add(Kit.Slider("阴影 Y 偏移", -8, 8, () => P.CountTextShadowOffsetY, v => Apply(() => P.CountTextShadowOffsetY = (float)v), "0.0"));

            p.Children.Add(Kit.Section("全键盘统一配色"));
            p.Children.Add(Kit.Check("Full108 使用独立配色", () => P.EnableFullKeyboardUnifiedColor,
                v => Apply(() => P.EnableFullKeyboardUnifiedColor = v)));
            if (P.EnableFullKeyboardUnifiedColor)
            {
                p.Children.Add(Kit.ColorRow("背景色", () => P.FullKeyboardBackground, v => Apply(() => P.FullKeyboardBackground = v)));
                p.Children.Add(Kit.ColorRow("按下背景色", () => P.FullKeyboardBackgroundClicked, v => Apply(() => P.FullKeyboardBackgroundClicked = v)));
                p.Children.Add(Kit.ColorRow("描边色", () => P.FullKeyboardOutline, v => Apply(() => P.FullKeyboardOutline = v)));
                p.Children.Add(Kit.ColorRow("按下描边色", () => P.FullKeyboardOutlineClicked, v => Apply(() => P.FullKeyboardOutlineClicked = v)));
                p.Children.Add(Kit.ColorRow("文字色", () => P.FullKeyboardText, v => Apply(() => P.FullKeyboardText = v)));
                p.Children.Add(Kit.ColorRow("按下文字色", () => P.FullKeyboardTextClicked, v => Apply(() => P.FullKeyboardTextClicked = v)));
            }
        }

        // ---- 5. 雨线 ----

        private void BuildRain(Panel p)
        {
            p.Children.Add(Kit.Section("总开关"));
            p.Children.Add(Kit.Check("启用雨线效果", () => P.EnableRainEffect, v => Apply(() => P.EnableRainEffect = v, true)));
            p.Children.Add(Kit.Check("松开后淡出", () => P.EnableRainFade, v => Apply(() => P.EnableRainFade = v)));
            p.Children.Add(Kit.Slider("淡出时长（秒）", 0.05, 3, () => P.RainFadeDuration, v => Apply(() => P.RainFadeDuration = (float)v), "0.00"));
            p.Children.Add(Kit.Check("顶部渐隐（渐变）", () => P.EnableRainGradient, v => Apply(() => P.EnableRainGradient = v)));
            p.Children.Add(Kit.Slider("渐隐像素", 0, 200, () => P.RainFadePx, v => Apply(() => P.RainFadePx = (float)v), "0"));
            p.Children.Add(Kit.Check("启用鬼键雨线", () => P.EnableGhostRain, v => Apply(() => P.EnableGhostRain = v)));

            for (int row = 0; row < 3; row++)
            {
                int r = row;
                p.Children.Add(Kit.Section((row + 1) + " 行雨线"));

                p.Children.Add(Kit.Check("该行启用雨线", () => RowEnabled(r), v => Apply(() => SetRowEnabled(r, v), true)));
                p.Children.Add(Kit.ColorRow("雨线颜色", () => RowColor(r), v => Apply(() => SetRowColor(r, v))));
                p.Children.Add(Kit.Slider("速度", 10, 600, () => RowSpeed(r), v => Apply(() => SetRowSpeed(r, (float)v)), "0"));
                p.Children.Add(Kit.Slider("轨道高度", 20, 800, () => RowHeight(r), v => Apply(() => SetRowHeight(r, (float)v)), "0"));
                p.Children.Add(Kit.Slider("宽度", 4, 120, () => RowWidth(r), v => Apply(() => SetRowWidth(r, (float)v)), "0"));
                p.Children.Add(Kit.Slider("起始 Y（相对键底边）", -400, 100, () => RowStartY(r), v => Apply(() => SetRowStartY(r, (float)v)), "0"));

                p.Children.Add(Kit.Check("雨线阴影", () => RowShadow(r), v => Apply(() => SetRowShadow(r, v))));
                if (RowShadow(r))
                {
                    p.Children.Add(Kit.ColorRow("阴影颜色", () => RowShadowColor(r), v => Apply(() => SetRowShadowColor(r, v))));
                    p.Children.Add(Kit.Slider("阴影 X 偏移", -20, 20, () => RowShadowX(r), v => Apply(() => SetRowShadowX(r, (float)v)), "0.0"));
                    p.Children.Add(Kit.Slider("阴影 Y 偏移", -20, 20, () => RowShadowY(r), v => Apply(() => SetRowShadowY(r, (float)v)), "0.0"));
                }

                if (r == 0)
                {
                    p.Children.Add(Kit.ColorRow("鬼键雨线颜色", () => P.GhostRainColor, v => Apply(() => P.GhostRainColor = v)));
                }
                else if (r == 1)
                {
                    p.Children.Add(Kit.ColorRow("鬼键雨线颜色", () => P.GhostRainColor2, v => Apply(() => P.GhostRainColor2 = v)));
                }
                else
                {
                    p.Children.Add(Kit.ColorRow("鬼键雨线颜色", () => P.GhostRainColor3, v => Apply(() => P.GhostRainColor3 = v)));
                }
            }
        }

        private bool RowEnabled(int r) => r == 0 ? P.EnableRainForRow1 : r == 1 ? P.EnableRainForRow2 : P.EnableRainForRow3;
        private void SetRowEnabled(int r, bool v)
        {
            if (r == 0) P.EnableRainForRow1 = v; else if (r == 1) P.EnableRainForRow2 = v; else P.EnableRainForRow3 = v;
        }

        private KvColor RowColor(int r) => r == 0 ? P.RainColor : r == 1 ? P.RainColor2 : P.RainColor3;
        private void SetRowColor(int r, KvColor v)
        {
            if (r == 0) P.RainColor = v; else if (r == 1) P.RainColor2 = v; else P.RainColor3 = v;
        }

        private float RowSpeed(int r) => r == 0 ? P.RainSpeedRow1 : r == 1 ? P.RainSpeedRow2 : P.RainSpeedRow3;
        private void SetRowSpeed(int r, float v)
        {
            if (r == 0) P.RainSpeedRow1 = v; else if (r == 1) P.RainSpeedRow2 = v; else P.RainSpeedRow3 = v;
        }

        private float RowHeight(int r) => r == 0 ? P.RainHeightRow1 : r == 1 ? P.RainHeightRow2 : P.RainHeightRow3;
        private void SetRowHeight(int r, float v)
        {
            if (r == 0) P.RainHeightRow1 = v; else if (r == 1) P.RainHeightRow2 = v; else P.RainHeightRow3 = v;
        }

        private float RowWidth(int r) => r == 0 ? P.RainWidthRow1 : r == 1 ? P.RainWidthRow2 : P.RainWidthRow3;
        private void SetRowWidth(int r, float v)
        {
            if (r == 0) P.RainWidthRow1 = v; else if (r == 1) P.RainWidthRow2 = v; else P.RainWidthRow3 = v;
        }

        private float RowStartY(int r) => r == 0 ? P.RainStartYRow1 : r == 1 ? P.RainStartYRow2 : P.RainStartYRow3;
        private void SetRowStartY(int r, float v)
        {
            if (r == 0) P.RainStartYRow1 = v; else if (r == 1) P.RainStartYRow2 = v; else P.RainStartYRow3 = v;
        }

        private bool RowShadow(int r) => r == 0 ? P.EnableRainShadowRow1 : r == 1 ? P.EnableRainShadowRow2 : P.EnableRainShadowRow3;
        private void SetRowShadow(int r, bool v)
        {
            if (r == 0) P.EnableRainShadowRow1 = v;
            else if (r == 1) P.EnableRainShadowRow2 = v;
            else P.EnableRainShadowRow3 = v;
        }

        private KvColor RowShadowColor(int r) => r == 0 ? P.RainShadowColorRow1 : r == 1 ? P.RainShadowColorRow2 : P.RainShadowColorRow3;
        private void SetRowShadowColor(int r, KvColor v)
        {
            if (r == 0) P.RainShadowColorRow1 = v;
            else if (r == 1) P.RainShadowColorRow2 = v;
            else P.RainShadowColorRow3 = v;
        }

        private float RowShadowX(int r) => r == 0 ? P.RainShadowOffsetXRow1 : r == 1 ? P.RainShadowOffsetXRow2 : P.RainShadowOffsetXRow3;
        private void SetRowShadowX(int r, float v)
        {
            if (r == 0) P.RainShadowOffsetXRow1 = v;
            else if (r == 1) P.RainShadowOffsetXRow2 = v;
            else P.RainShadowOffsetXRow3 = v;
        }

        private float RowShadowY(int r) => r == 0 ? P.RainShadowOffsetYRow1 : r == 1 ? P.RainShadowOffsetYRow2 : P.RainShadowOffsetYRow3;
        private void SetRowShadowY(int r, float v)
        {
            if (r == 0) P.RainShadowOffsetYRow1 = v;
            else if (r == 1) P.RainShadowOffsetYRow2 = v;
            else P.RainShadowOffsetYRow3 = v;
        }

        // ---- 6. 按键绑定 ----

        private void BuildKeys(Panel p)
        {
            var style = P.StyleEnum;
            int need = KvGeometry.TotalKeyCount(style);
            var keys = P.KeysFor(style) ?? KvProfile.DefaultKeysFor(style);
            var ghost = GhostArrayFor(style);

            p.Children.Add(Kit.Section("主键位绑定（" + need + " 个）"));
            p.Children.Add(Kit.Hint("点一下按钮，然后按下要绑定的键。Esc 取消，点空白处放弃。"));

            var wrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            for (int i = 0; i < need; i++)
            {
                int idx = i;
                wrap.Children.Add(CompactKeyButton(
                    idx.ToString(),
                    () => ReadInt(keys, idx),
                    v => Apply(() =>
                    {
                        var arr = P.KeysFor(P.StyleEnum);
                        if (arr == null || idx >= arr.Length) return;
                        arr[idx] = v;
                    })));
            }
            p.Children.Add(wrap);

            p.Children.Add(Kit.HRow(
                Kit.Button("恢复本样式默认键位", () =>
                {
                    if (MessageBox.Show(this, "把当前样式的主键位恢复为默认值？", "CKeyViewer",
                            MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                    Apply(() =>
                    {
                        var def = KvProfile.DefaultKeysFor(P.StyleEnum);
                        P.SetKeysFor(P.StyleEnum, (int[])def.Clone());
                    }, true);
                })
            ));

            if (ghost != null)
            {
                p.Children.Add(Kit.Section("鬼键绑定（这些键只触发雨线，不计数）"));
                var gw = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
                for (int i = 0; i < ghost.Length; i++)
                {
                    int idx = i;
                    gw.Children.Add(CompactKeyButton(
                        "G" + idx,
                        () => ReadInt(ghost, idx),
                        v => Apply(() => { if (idx < ghost.Length) ghost[idx] = v; })));
                }
                p.Children.Add(gw);
            }

            p.Children.Add(Kit.Section("键名文字"));
            p.Children.Add(Kit.Hint("留空则显示键码的标准名称（如 A、Space、F1）。"));
            var texts = P.KeyTextsFor(style);
            if (texts != null)
            {
                var tw = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
                for (int i = 0; i < Math.Min(texts.Length, need); i++)
                {
                    int idx = i;
                    var box = new TextBox
                    {
                        Width = 48,
                        Margin = new Thickness(0, 0, 6, 6),
                        Padding = new Thickness(4, 2, 4, 2),
                        FontSize = 12,
                        Background = Kit.PanelAlt,
                        Foreground = Kit.Text,
                        BorderBrush = Kit.Border,
                        Text = texts[idx] ?? "",
                        ToolTip = idx + " : " + Kit.KeyLabel(ReadInt(keys, idx))
                    };
                    box.LostFocus += (s, e) => Apply(() =>
                    {
                        var arr = P.KeyTextsFor(P.StyleEnum);
                        if (arr != null && idx < arr.Length) arr[idx] = box.Text;
                    });
                    box.KeyDown += (s, e) => { if (e.Key == Key.Enter) Keyboard.ClearFocus(); };
                    tw.Children.Add(box);
                }
                p.Children.Add(tw);
            }

            // ---- 脚键 ----
            if (P.FootStyleEnum != FootKeyviewerStyle.None)
            {
                var fkeys = P.FootKeysFor(P.FootStyleEnum) ?? Array.Empty<int>();
                p.Children.Add(Kit.Section("脚键绑定（" + fkeys.Length + " 个）"));

                var fw = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
                for (int i = 0; i < fkeys.Length; i++)
                {
                    int idx = i;
                    fw.Children.Add(CompactKeyButton(
                        "脚" + idx,
                        () => ReadInt(fkeys, idx),
                        v => Apply(() =>
                        {
                            var arr = P.FootKeysFor(P.FootStyleEnum);
                            if (arr != null && idx < arr.Length) arr[idx] = v;
                        })));
                }
                p.Children.Add(fw);
            }
        }

        private static int ReadInt(int[] arr, int i) => (arr != null && i >= 0 && i < arr.Length) ? arr[i] : 0;

        private int[] GhostArrayFor(KeyviewerStyle style)
        {
            switch (style)
            {
                case KeyviewerStyle.Key8: return P.GhostKey8;
                case KeyviewerStyle.Key10: return P.GhostKey10;
                case KeyviewerStyle.Key12: return P.GhostKey12;
                case KeyviewerStyle.Key14: return P.GhostKey14;
                case KeyviewerStyle.Key16: return P.GhostKey16;
                case KeyviewerStyle.Key20: return P.GhostKey20;
                case KeyviewerStyle.Key24: return P.GhostKey24;
                default: return null;
            }
        }

        private static Button CompactKeyButton(string caption, Func<int> get, Action<int> set)
        {
            var btn = new Button
            {
                MinWidth = 62,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(6, 3, 6, 3),
                Background = Kit.PanelAlt,
                BorderBrush = Kit.Border,
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                Foreground = Kit.Text,
                Cursor = Cursors.Hand
            };

            // 捕获逻辑与 KeyRow 共用 Kit.KeyCapture：支持修饰键与鼠标左右中侧键
            Kit.KeyCapture capture = null;

            void Show()
            {
                btn.Content = caption + "  " + ((capture != null && capture.IsCapturing) ? "…" : Kit.KeyLabel(get()));
            }

            capture = new Kit.KeyCapture(set, Show);
            capture.Attach(btn);

            Show();
            return btn;
        }

        // ---- 7. 每键配色 ----

        private void BuildPerKey(Panel p)
        {
            p.Children.Add(Kit.Section("每键独立配色"));
            p.Children.Add(Kit.Check("启用每键配色（覆盖全局配色）", () => P.EnablePerKeyColors,
                v => Apply(() => P.EnablePerKeyColors = v, true)));
            p.Children.Add(Kit.Hint("共 42 个槽位：0..23 为主键，24..39 为脚键（Key24 及以下布局），40/41 预留。\r\n" +
                                    "Full108 布局下这 42 个槽位直接对应前 42 个键位（与原版一致）。"));

            if (!P.EnablePerKeyColors)
            {
                p.Children.Add(Kit.Hint("开启后这里会显示每个槽位的颜色按钮。"));
                return;
            }

            p.Children.Add(Kit.HRow(
                Kit.Button("全部套用全局配色", () =>
                {
                    Apply(() =>
                    {
                        for (int i = 0; i < KvProfile.PerKeySlots; i++)
                        {
                            SetPerKey(P.PerKeyBackground, i, P.Background);
                            SetPerKey(P.PerKeyBackgroundClicked, i, P.BackgroundClicked);
                            SetPerKey(P.PerKeyOutline, i, P.Outline);
                            SetPerKey(P.PerKeyOutlineClicked, i, P.OutlineClicked);
                            SetPerKey(P.PerKeyText, i, P.Text);
                            SetPerKey(P.PerKeyTextClicked, i, P.TextClicked);
                            SetPerKey(P.PerKeyRainColor, i, P.RainColor);
                        }
                    }, true);
                })
            ));

            for (int i = 0; i < KvProfile.PerKeySlots; i++)
            {
                int idx = i;
                string name = SlotName(idx);

                p.Children.Add(Kit.Section(idx + "  " + name));
                p.Children.Add(Kit.ColorRow("背景色", () => Pick(P.PerKeyBackground, idx, P.Background),
                    v => Apply(() => SetPerKey(P.PerKeyBackground, idx, v))));
                p.Children.Add(Kit.ColorRow("按下背景色", () => Pick(P.PerKeyBackgroundClicked, idx, P.BackgroundClicked),
                    v => Apply(() => SetPerKey(P.PerKeyBackgroundClicked, idx, v))));
                p.Children.Add(Kit.ColorRow("描边色", () => Pick(P.PerKeyOutline, idx, P.Outline),
                    v => Apply(() => SetPerKey(P.PerKeyOutline, idx, v))));
                p.Children.Add(Kit.ColorRow("按下描边色", () => Pick(P.PerKeyOutlineClicked, idx, P.OutlineClicked),
                    v => Apply(() => SetPerKey(P.PerKeyOutlineClicked, idx, v))));
                p.Children.Add(Kit.ColorRow("文字色", () => Pick(P.PerKeyText, idx, P.Text),
                    v => Apply(() => SetPerKey(P.PerKeyText, idx, v))));
                p.Children.Add(Kit.ColorRow("按下文字色", () => Pick(P.PerKeyTextClicked, idx, P.TextClicked),
                    v => Apply(() => SetPerKey(P.PerKeyTextClicked, idx, v))));
            }

            p.Children.Add(Kit.Section("每键字号"));
            p.Children.Add(Kit.Check("启用每键独立字号（0 = 沿用全局）", () => P.EnablePerKeyTextSize,
                v => Apply(() => P.EnablePerKeyTextSize = v, true)));
            if (P.EnablePerKeyTextSize)
            {
                for (int i = 0; i < KvProfile.PerKeySlots; i++)
                {
                    int idx = i;
                    p.Children.Add(Kit.Slider(idx + "  " + SlotName(idx), 0, 90,
                        () => PickF(P.PerKeyFontSize, idx), v => Apply(() => SetPerKeyF(P.PerKeyFontSize, idx, (float)v)), "0"));
                }
            }
        }

        private string SlotName(int idx)
        {
            // 脚键占 24..39，且只有非全键盘布局才会出现
            if (!KvGeometry.IsFullKeyboard(P.StyleEnum) &&
                idx >= KvGeometry.FootKeyBase && idx < KvGeometry.FootKeyBase + 16)
            {
                int fi = idx - KvGeometry.FootKeyBase;
                var fk = P.FootKeysFor(P.FootStyleEnum);
                if (fk != null && fi < fk.Length) return "脚键 " + Kit.KeyLabel(fk[fi]);
                return "脚键 " + fi;
            }

            var keys = P.KeysFor(P.StyleEnum);
            if (keys != null && idx < keys.Length) return Kit.KeyLabel(keys[idx]);
            return "（未使用）";
        }

        private static KvColor Pick(List<KvColor> list, int i, KvColor fb)
        {
            if (list == null || i < 0 || i >= list.Count) return fb;
            return list[i];
        }

        private static float PickF(List<float> list, int i)
        {
            if (list == null || i < 0 || i >= list.Count) return 0;
            return list[i];
        }

        private static void SetPerKey(List<KvColor> list, int i, KvColor v)
        {
            if (list == null || i < 0) return;
            while (list.Count <= i) list.Add(v);
            list[i] = v;
        }

        private static void SetPerKeyF(List<float> list, int i, float v)
        {
            if (list == null || i < 0) return;
            while (list.Count <= i) list.Add(0f);
            list[i] = v;
        }

        // ---- 8. 按压动画 ----

        private void BuildAnimation(Panel p)
        {
            p.Children.Add(Kit.Section("按压缩放动画"));
            p.Children.Add(Kit.Check("启用按压动画", () => P.EnablePressAnimation,
                v => Apply(() => P.EnablePressAnimation = v, true)));

            if (!P.EnablePressAnimation) return;

            p.Children.Add(Kit.Slider("按下缩放比例", 0.3, 1.0, () => P.PressAnimationScale,
                v => Apply(() => P.PressAnimationScale = (float)v), "0.00"));
            p.Children.Add(Kit.Slider("动画时长（毫秒）", 10, 600, () => P.PressAnimationDurationMs,
                v => Apply(() => P.PressAnimationDurationMs = (float)v), "0"));
            p.Children.Add(Kit.Combo("缓动曲线", KvEasing.Names.ToList(),
                () => KvEasing.IndexOf(P.PressAnimationEasing),
                i => Apply(() => P.PressAnimationEasing = KvEasing.Names[i]), 260));
            p.Children.Add(Kit.Check("同时缩放雨线", () => P.EnablePressAnimationOnRain,
                v => Apply(() => P.EnablePressAnimationOnRain = v)));

            p.Children.Add(Kit.Hint("缓动曲线名称与原版完全一致，可直接写入配置文件。"));
        }

        // ---- 9. 统计 ----

        private void BuildStats(Panel p)
        {
            p.Children.Add(Kit.Section("统计"));
            p.Children.Add(Kit.Hint(string.Format(
                "累计按键总数：{0:N0}\r\n每秒按键（KPS）：{1}\r\n当前活跃雨线：{2}\r\n覆盖层可见：{3}\r\n位置：{4:N0} × {5:N0} 单位",
                _host.TotalCount, _host.TotalKps, _host.Rain.ActiveCount,
                _host.Visible ? "是" : "否", _host.Store.Profile.Size * 100, 100)));

            p.Children.Add(Kit.HRow(
                Kit.Button("刷新", () => Rebuild()),
                Kit.Button("重置计数", () =>
                {
                    if (MessageBox.Show(this, "把所有按键计数归零？", "CKeyViewer",
                            MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                    {
                        _host.ResetCounts();
                        UpdateHeader();
                        Rebuild();
                    }
                })
            ));

            p.Children.Add(Kit.Section("KPS / Total 条（Full108 专用）"));
            if (KvGeometry.IsFullKeyboard(P.StyleEnum))
            {
                p.Children.Add(Kit.Check("显示 KPS / Total", () => P.FullKeyboardShowKpsTotal,
                    v => Apply(() => P.FullKeyboardShowKpsTotal = v)));
                p.Children.Add(Kit.Check("居中", () => P.KpsTotalCentered, v => Apply(() => P.KpsTotalCentered = v)));
                p.Children.Add(Kit.Check("居中时上下堆叠", () => P.KpsTotalStackedWhenCentered,
                    v => Apply(() => P.KpsTotalStackedWhenCentered = v)));
                p.Children.Add(Kit.Slider("尺寸", 40, 400, () => P.FullKeyboardKpsTotalSize,
                    v => Apply(() => P.FullKeyboardKpsTotalSize = (float)v), "0"));
                p.Children.Add(Kit.Slider("KPS 条 X（0=左 1=右）", 0, 1, () => P.FullKpsPosition.x,
                    v => Apply(() => P.FullKpsPosition = KvPos.Of((float)v, P.FullKpsPosition.y)), "0.000"));
                p.Children.Add(Kit.Slider("KPS 条 Y（0=顶 1=底）", 0, 1, () => P.FullKpsPosition.y,
                    v => Apply(() => P.FullKpsPosition = KvPos.Of(P.FullKpsPosition.x, (float)v)), "0.000"));
                p.Children.Add(Kit.Slider("Total 条 X", 0, 1, () => P.FullTotalPosition.x,
                    v => Apply(() => P.FullTotalPosition = KvPos.Of((float)v, P.FullTotalPosition.y)), "0.000"));
                p.Children.Add(Kit.Slider("Total 条 Y", 0, 1, () => P.FullTotalPosition.y,
                    v => Apply(() => P.FullTotalPosition = KvPos.Of(P.FullTotalPosition.x, (float)v)), "0.000"));
            }
            else
            {
                p.Children.Add(Kit.Hint("仅 Full108 布局下可调。"));
            }
        }

        // ---- 10. 热键信息 ----

        private void BuildInfo(Panel p)
        {
            p.Children.Add(Kit.Section("全局热键"));
            var hk = KvHotkeys.Current;
            if (hk == null)
            {
                p.Children.Add(Kit.Hint("热键尚未初始化。"));
            }
            else
            {
                p.Children.Add(Kit.Hint(
                    (hk.ToggleRegistered ? "✔ " : "✖ ") + hk.ToggleKeyName + "    显示 / 隐藏覆盖层\r\n" +
                    (hk.ResetRegistered ? "✔ " : "✖ ") + hk.ResetKeyName + "    所有计数归零\r\n" +
                    (hk.NextProfileRegistered ? "✔ " : "✖ ") + hk.NextProfileKeyName + "    切到下一个档案\r\n" +
                    (hk.LayoutRegistered ? "✔ " : "✖ ") + hk.LayoutKeyName + "    进入 / 退出自由布局拖动模式\r\n" +
                    (hk.SettingsRegistered ? "✔ " : "✖ ") + hk.SettingsKeyName + "    打开本设置窗口\r\n\r\n" +
                    "标记 ✖ 的组合已被其它程序占用，本程序会自动改用备选功能键。"));
            }

            p.Children.Add(Kit.Section("关于"));
            p.Children.Add(Kit.Hint(
                "CKeyViewer —— 复刻 Jipper/JipperKeyViewer 的按键可视化覆盖层。\r\n" +
                "配置格式与 jipper 完全兼容：config/settings.json 与 config/profiles/<名称>.json，\r\n" +
                "首次启动会自动从相邻的 jipper/config 迁移原有档案。\r\n\r\n" +
                "渲染模型与原版一致：参考画布高度恒为 1080 单位，Size 是整体缩放，\r\n" +
                "位置使用 0..1 归一化坐标。"));

            p.Children.Add(Kit.Section("程序信息"));
            p.Children.Add(Kit.Hint(string.Format(
                "配置文件：{0}\r\n版本：{1}\r\nDPI 缩放：{2:0.###}\r\n覆盖层尺寸：{3:0.#} × {4:0.#} DIP",
                _host.Store.ProfilePath(_host.Store.CurrentProfile),
                typeof(KvSettingsWindow).Assembly.GetName().Version,
                _host.DpiScale, ActualWidth, ActualHeight)));
        }

        // ---------------------------------------------------------------
        // 字体列表
        // ---------------------------------------------------------------

        private static List<string> FontChoices()
        {
            var list = new List<string> { "MapleStory", "cjkFonts-regular-normalized", "Segoe UI" };

            try
            {
                foreach (var dir in KvFonts.SearchDirs())
                {
                    IEnumerable<string> files;
                    try { files = Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly); }
                    catch { continue; }

                    foreach (var f in files)
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext != ".otf" && ext != ".ttf" && ext != ".ttc") continue;
                        string name = Path.GetFileNameWithoutExtension(f);
                        if (!list.Contains(name)) list.Add(name);
                    }
                }
            }
            catch { }

            return list.Distinct().ToList();
        }
    }

    /// <summary>极简文本输入对话框（WPF 没有内置 InputBox）。</summary>
    internal static class InputDialog
    {
        public static string Ask(Window owner, string title, string prompt, string initial)
        {
            var win = new Window
            {
                Title = title,
                Width = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = Kit.Bg,
                Foreground = Kit.Text,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI")
            };

            var sp = new StackPanel { Margin = new Thickness(14) };
            sp.Children.Add(Kit.Text2(prompt, 12.5));

            var box = new TextBox
            {
                Text = initial ?? "",
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(6, 4, 6, 4),
                Background = Kit.PanelAlt,
                Foreground = Kit.Text,
                BorderBrush = Kit.Border
            };
            sp.Children.Add(box);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var ok = Kit.Button("确定", () => { win.DialogResult = true; }, accent: true);
            var cancel = Kit.Button("取消", () => { win.DialogResult = false; });
            cancel.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            sp.Children.Add(buttons);

            win.Content = sp;
            win.Resources = Kit.Theme();
            box.SelectAll();
            box.Focus();

            return win.ShowDialog() == true ? box.Text.Trim() : null;
        }
    }
}

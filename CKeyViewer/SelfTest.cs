using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CKeyViewer.Core;
using CKeyViewer.Ui;

namespace CKeyViewer
{
    /// <summary>
    /// 无界面自检 —— 用「假的按键状态」跑一遍按键捕获的扫描逻辑。
    ///
    /// 为什么做成内置开关而不是外部脚本：验证捕获需要真实按键，
    /// 而模拟键鼠会抢走用户当前的输入（用户可能正开着游戏或编辑器），太打扰。
    /// 把扫描逻辑抽成纯函数 <see cref="Kit.KeyCapture.PickKey"/> 之后，
    /// 直接喂一组 VK 就能覆盖到全部分支。
    ///
    /// 用法：<c>CKeyViewer.exe --selftest [输出文件]</c>
    /// </summary>
    internal static class SelfTest
    {
        private static readonly StringBuilder Out = new StringBuilder();
        private static int _pass, _fail;

        public static int Run(string outPath)
        {
            Out.AppendLine("CKeyViewer self-test");
            Out.AppendLine("---- key capture ----");

            // 单按修饰键 —— 这是用户报的 bug：以前 IsModifier 直接 return，永远绑不上
            Capture("LeftShift", 304, Keys(0x10, 0xA0));
            Capture("RightShift", 303, Keys(0x10, 0xA1));
            Capture("LeftControl", 306, Keys(0x11, 0xA2));
            Capture("RightControl", 305, Keys(0x11, 0xA3));
            Capture("LeftAlt", 308, Keys(0x12, 0xA4));
            Capture("RightAlt", 307, Keys(0x12, 0xA5));

            // 鼠标 —— 键盘事件根本不来，只能靠轮询
            Capture("Mouse0(左键)", 323, Keys(0x01));
            Capture("Mouse1(右键)", 324, Keys(0x02));
            Capture("Mouse2(中键)", 325, Keys(0x04));
            Capture("Mouse3(侧键1)", 326, Keys(0x05));
            Capture("Mouse4(侧键2)", 327, Keys(0x06));

            // 普通键别被改坏
            Capture("A", 97, Keys(0x41));
            Capture("Space", 32, Keys(0x20));
            Capture("F5", 286, Keys(0x74));
            Capture("Numpad0", 256, Keys(0x60));

            // 左键还按着就进捕获：必须被「记账」挡住，否则一进捕获就绑成 Mouse0
            CaptureBlocked("起始已按下的左键被忽略", Keys(0x01, 0xA0), Keys(0x01), 304);

            // 抬起来之后就解除记账
            CaptureAfterRelease("左键抬起后再按可以绑 Mouse0");

            // 泛用键映射不到 Unity KeyCode，不能绑出个空值
            Capture("泛用 VK_SHIFT 单独亮 → 不捕获", 0, Keys(0x10));
            Capture("泛用 VK_CONTROL 单独亮 → 不捕获", 0, Keys(0x11));

            // Esc 在 Poll 层被单独吃掉（取消），PickKey 里也不该返回它
            Capture("Esc 不参与捕获", 0, Keys(0x1B));

            Out.AppendLine("---- adofai settings ----");
            AdofaiRoundTrip();
            AdofaiSanitize();

            Out.AppendLine("---- snap anchors ----");
            SnapAnchors();

            Out.AppendLine("---- adofai element layout ----");
            AdofaiElementLayout();

            Out.AppendLine("---- tray menu skin ----");
            MenuSkin();

            Out.AppendLine("---- combo (dropdown) templates ----");
            ComboTemplates();

            Out.AppendLine("---- summary ----");
            Out.AppendLine(_fail == 0 ? string.Format("ALL PASS ({0} checks)", _pass)
                                      : string.Format("{0} passed, {1} FAILED", _pass, _fail));
            Out.AppendLine("---- mapping ----");
            Out.AppendLine("LeftShift   unity=304 vk=0x" + KeyCodeMap.ToVirtualKey(304).ToString("X2"));
            Out.AppendLine("Mouse0      unity=323 vk=0x" + KeyCodeMap.ToVirtualKey(323).ToString("X2"));
            Out.AppendLine("Mouse1      unity=324 vk=0x" + KeyCodeMap.ToVirtualKey(324).ToString("X2"));
            Out.AppendLine("KeyBind 名   " + KeyCodeMap.NameOf(304) + " / " + KeyCodeMap.NameOf(323)
                            + " / " + KeyCodeMap.NameOf(324));
            Out.AppendLine("键帽标签     " + KeyCodeMap.DisplayName(304) + " / " + KeyCodeMap.DisplayName(323)
                            + " / " + KeyCodeMap.DisplayName(324));

            string text = Out.ToString();
            Console.Write(text);
            try { System.IO.File.WriteAllText(outPath, text); }
            catch (Exception ex) { Console.WriteLine("write " + outPath + ": " + ex.Message); }

            return _fail == 0 ? 0 : 1;
        }

        // ---- 断言 ----

        private static void Capture(string what, int expect, HashSet<int> down)
        {
            var held = new HashSet<int>();
            int got = Kit.KeyCapture.PickKey(vk => down.Contains(vk), held);
            Report(what, expect, got);
        }

        /// <summary>起始键盘里已经按着一批键（<paramref name="held"/>），此时应捕获 <paramref name="expect"/>。</summary>
        private static void CaptureBlocked(string what, HashSet<int> down, HashSet<int> held, int expect)
        {
            var h = new HashSet<int>(held);
            int got = Kit.KeyCapture.PickKey(vk => down.Contains(vk), h);
            Report(what, expect, got);
        }

        /// <summary>左键先按住 → 抬起 → 再按，应该能绑成 Mouse0。</summary>
        private static void CaptureAfterRelease(string what)
        {
            var held = new HashSet<int>();
            var left = Keys(0x01);
            Kit.KeyCapture.Snapshot(vk => left.Contains(vk), held);   // 按住左键开始捕获
            int during = Kit.KeyCapture.PickKey(vk => left.Contains(vk), held);   // 还按着 → 不该捕获
            int afterUp = Kit.KeyCapture.PickKey(vk => false, held);              // 抬起
            int again = Kit.KeyCapture.PickKey(vk => left.Contains(vk), held);    // 再按下
            bool ok = during == 0 && afterUp == 0 && again == 323;
            Report(what + "（按下={0} 抬起={1} 再按={2}）".Replace("{0}", during.ToString())
                   .Replace("{1}", afterUp.ToString()).Replace("{2}", again.ToString()),
                   323, ok ? 323 : again);
        }

        // ---- ADOFAI 配置 ----

        /// <summary>
        /// JSON 往返。这一条不是形式主义：System.Text.Json 默认**不序列化公开字段**，
        /// 早先把 AdofaiSettings 写成字段时 adofai.json 会静静变成 `{}`，
        /// 表现是「设置页改完重启全丢」。全改成属性之后靠这个用例钉住。
        /// </summary>
        private static void AdofaiRoundTrip()
        {
            var src = new Adofai.AdofaiSettings
            {
                Enabled = true,
                X = 0.375, Y = 0.8125, Align = 2, FontSize = 31.5,
                Bold = false, Italic = true, FontRef = "小明体",
                Color = KvColor.Rgba(1f, 0f, 0.25f, 0.75f),
                LabelColor = KvColor.Rgba(0.1f, 0.2f, 0.3f, 0.4f),
                TitleColor = KvColor.Rgba(1f, 0.85f, 0.3f, 1f),
                ShowCombo = false, ShowTitle = false, ShowAccuracy = false, ShowXAccuracy = false,
                ShowProgress = false, ShowBpm = false, ShowJudgement = false, ShowStats = false,
                AllowElCombo = false, AllowAutoCombo = false,
                LineGap = 1.75, HasOutline = false,
                OutlineColor = KvColor.Rgba(0.5f, 0.5f, 0.5f, 1f), OutlineWidth = 0.125
            };

            string json = System.Text.Json.JsonSerializer.Serialize(src, KvProfileStore.JsonOptions);
            var back = System.Text.Json.JsonSerializer.Deserialize<Adofai.AdofaiSettings>(
                json, KvProfileStore.JsonOptions);

            Check("AdofaiSettings JSON 往返不为空", back != null);
            if (back == null) return;

            var diffs = new List<string>();
            void Eq(string n, object a, object b)
            {
                if (!Equals(a, b)) diffs.Add(n + " " + a + "!=" + b);
            }
            Eq("Enabled", src.Enabled, back.Enabled);
            Eq("X", src.X, back.X);
            Eq("Y", src.Y, back.Y);
            Eq("Align", src.Align, back.Align);
            Eq("FontSize", src.FontSize, back.FontSize);
            Eq("Bold", src.Bold, back.Bold);
            Eq("Italic", src.Italic, back.Italic);
            Eq("FontRef", src.FontRef, back.FontRef);
            Eq("Color", src.Color.ToHex(), back.Color.ToHex());
            Eq("LabelColor", src.LabelColor.ToHex(), back.LabelColor.ToHex());
            Eq("TitleColor", src.TitleColor.ToHex(), back.TitleColor.ToHex());
            Eq("ShowCombo", src.ShowCombo, back.ShowCombo);
            Eq("ShowTitle", src.ShowTitle, back.ShowTitle);
            Eq("ShowAccuracy", src.ShowAccuracy, back.ShowAccuracy);
            Eq("ShowXAccuracy", src.ShowXAccuracy, back.ShowXAccuracy);
            Eq("ShowProgress", src.ShowProgress, back.ShowProgress);
            Eq("ShowBpm", src.ShowBpm, back.ShowBpm);
            Eq("ShowJudgement", src.ShowJudgement, back.ShowJudgement);
            Eq("ShowStats", src.ShowStats, back.ShowStats);
            Eq("AllowElCombo", src.AllowElCombo, back.AllowElCombo);
            Eq("AllowAutoCombo", src.AllowAutoCombo, back.AllowAutoCombo);
            Eq("LineGap", src.LineGap, back.LineGap);
            Eq("HasOutline", src.HasOutline, back.HasOutline);
            Eq("OutlineColor", src.OutlineColor.ToHex(), back.OutlineColor.ToHex());
            Eq("OutlineWidth", src.OutlineWidth, back.OutlineWidth);

            Check("字段无一丢失（" + (diffs.Count == 0 ? "全部一致" : string.Join("; ", diffs)) + "）",
                diffs.Count == 0);

            // 出厂默认必须是关闭的：没装游戏的人不该看见它在到处找进程
            Check("默认不启用", !new Adofai.AdofaiSettings().Enabled);
            Check("默认开启连击数", new Adofai.AdofaiSettings().ShowCombo);
        }

        /// <summary>越界值要被收敛，不能让配置文件里的脏数据把覆盖层画到屏幕外。</summary>
        private static void AdofaiSanitize()
        {
            var s = new Adofai.AdofaiSettings
            {
                X = 5, Y = -3, Align = 9, FontSize = 0, LineGap = 0, OutlineWidth = 99, FontRef = null
            };
            s.Sanitize();

            Check("X 夹到 1（实际 " + s.X + "）", s.X == 1);
            Check("Y 夹到 0（实际 " + s.Y + "）", s.Y == 0);
            Check("Align 越界回落到默认值 1（实际 " + s.Align + "）", s.Align == 1);
            Check("字号有下限（实际 " + s.FontSize + "）", s.FontSize >= 4);
            Check("行间距有下限（实际 " + s.LineGap + "）", s.LineGap >= 0.6);
            Check("描边有上限（实际 " + s.OutlineWidth + "）", s.OutlineWidth <= 0.5);
            Check("FontRef 不为 null", s.FontRef != null);

            // 文案：null / 空都要回落到默认，模板写坏了不能抛异常
            var lab = new Adofai.AdofaiLabels { Acc = null, Stats = "" };
            lab.Sanitize();
            Check("标签为 null 时回落默认（" + lab.Acc + "）", lab.Acc == "ACC");
            Check("模板为空时回落默认", lab.Stats.Contains("{0}"));
            Check("模板正常套用",
                new Adofai.AdofaiLabels().StatsLine(3, 1, 4) == "Deaths 3   CP 1   Try 4");
            var broken = new Adofai.AdofaiLabels { Stats = "死了{9}次" };
            Check("模板占位符越界时回落默认",
                broken.StatsLine(2, 0, 3) == "Deaths 2   CP 0   Try 3");

            var s2 = new Adofai.AdofaiSettings { Labels = null };
            s2.Sanitize();
            Check("Labels 为 null 时自动补上", s2.Labels != null && s2.Labels.Bpm == "BPM");
        }

        private static void Check(string what, bool ok)
        {
            if (ok) _pass++; else _fail++;
            Out.AppendLine(string.Format("  {0}  {1}", ok ? "OK  " : "FAIL", what));
        }

        // ---- 吸附 ----

        /// <summary>
        /// 吸附算法是纯函数，直接喂矩形就能覆盖全部分支。
        /// 重点钉住三件事：九个位置算得对、**绝不改尺寸**、块比屏幕大时不越界。
        /// </summary>
        private static void SnapAnchors()
        {
            var area = new Rect(0, 0, 1000, 800);
            double l, t;

            Check("自由（0）不吸附", !KvSnap.Place(0, 10, 100, 50, area, out l, out t));
            Check("左上（1）", KvSnap.Place(1, 10, 100, 50, area, out l, out t) && l == 10 && t == 10);
            Check("中上（2）", KvSnap.Place(2, 10, 100, 50, area, out l, out t) && l == 450 && t == 10);
            Check("右上（3）", KvSnap.Place(3, 10, 100, 50, area, out l, out t) && l == 890 && t == 10);
            Check("左中（4）", KvSnap.Place(4, 10, 100, 50, area, out l, out t) && l == 10 && t == 375);
            Check("正中（5）", KvSnap.Place(5, 10, 100, 50, area, out l, out t) && l == 450 && t == 375);
            Check("右中（6）", KvSnap.Place(6, 10, 100, 50, area, out l, out t) && l == 890 && t == 375);
            Check("左下（7）", KvSnap.Place(7, 10, 100, 50, area, out l, out t) && l == 10 && t == 740);
            Check("中下（8）", KvSnap.Place(8, 10, 100, 50, area, out l, out t) && l == 450 && t == 740);
            Check("右下（9）", KvSnap.Place(9, 10, 100, 50, area, out l, out t) && l == 890 && t == 740);

            // 越界的锚点值要被收敛，配置文件里写脏了也不能画到屏幕外
            Check("锚点 99 收敛成 9",
                KvSnap.Place(99, 10, 100, 50, area, out l, out t) && l == 890 && t == 740);
            Check("锚点 -5 收敛成自由", !KvSnap.Place(-5, 10, 100, 50, area, out l, out t));

            // 块比工作区还大：夹到左上角，绝不能出现负坐标
            Check("超大块不越界",
                KvSnap.Place(9, 10, 2000, 1600, area, out l, out t) && l == 0 && t == 0);

            // 主屏不是从 0,0 开始（副屏在左 / 任务栏在左）时，坐标要跟着工作区走
            var shifted = new Rect(-1920, 40, 1920, 1000);
            Check("工作区有偏移时跟着走",
                KvSnap.Place(7, 0, 100, 50, shifted, out l, out t) && l == -1920 && t == 990);
        }

        // ---- 托盘菜单皮肤 ----

        /// <summary>
        /// 托盘菜单换了自定义渲染器（<see cref="KvMenuRenderer"/>）。这里只验证能构造、
        /// 以及调色板确实传到了 <c>ProfessionalColorTable</c> —— 剩下的绘制是 GDI，
        /// 无窗口环境下没法跑，靠截图核对（tools/runapp.py --env CKV_TRAYMENU=）。
        /// </summary>
        private static void MenuSkin()
        {
            foreach (var pal in new[] { KvPalette.Light, KvPalette.Dark })
            {
                string which = pal.IsDark ? "深色" : "浅色";
                var r = new KvMenuRenderer(pal);
                Check("菜单渲染器可构造（" + which + "）", r != null && r.ColorTable != null);

                // 卡片底色必须真的走到 ColorTable 上 —— 渲染器与 ColorTable 脱钩过，
                // 症状就是「浅色主题下菜单是一张纯白卡片，文字全看不见」
                var want = System.Drawing.ColorTranslator.FromHtml(pal.Card);
                Check("菜单卡片底色跟调色板一致（" + which + "）",
                    r.ColorTable.ToolStripDropDownBackground.ToArgb() == want.ToArgb());

                var accent = System.Drawing.ColorTranslator.FromHtml(pal.AccentSoft);
                Check("菜单高亮底色跟调色板一致（" + which + "）",
                    r.ColorTable.MenuItemSelected.ToArgb() == accent.ToArgb());
            }

            // 菜单里的「吸附位置」和设置面板共用同一份名称表，不能再各写一份
            for (int i = KvSnap.Min; i <= KvSnap.Max; i++)
                Check("锚点 " + i + " 有名字", !string.IsNullOrEmpty(KvSnap.NameOf(i)));

            Check("锚点名称表与取值域等长", KvSnap.Names.Length == KvSnap.Max - KvSnap.Min + 1);
            Check("锚点 0 是自由", KvSnap.NameOf(KvSnap.Min) == "自由");
        }

        // ---- 下拉菜单（ContextMenu）模板 ----

        /// <summary>
        /// 下拉菜单**离屏渲染**。不这么做的话，这一段只能靠「用户说它是张白卡片」来发现 ——
        /// WPF 的 ContextMenu 模板一旦没给 / 给错，症状是整个下拉退回系统默认那套
        /// 白底方角，和旁边的 iOS 面板完全不是一回事，而代码里看不出任何异常。
        /// <para>
        /// 真实点击弹窗在无头环境下截不到（ContextMenu 要拿鼠标捕获，
        /// 合成事件拿不到就会被立刻关掉），所以改成把模板套到普通元素上
        /// 用 <see cref="System.Windows.Media.Imaging.RenderTargetBitmap"/> 画出来，
        /// 顺便存成 PNG 供人工核对。
        /// </para>
        /// </summary>
        private static void ComboTemplates()
        {
            foreach (var dark in new[] { true, false })
            {
                Kit.UseTheme(dark);
                string which = dark ? "深色" : "浅色";
                var dict = Kit.Theme();

                var cm = FindStyle(dict, typeof(System.Windows.Controls.ContextMenu));
                var mi = FindStyle(dict, typeof(System.Windows.Controls.MenuItem));
                Check("下拉菜单有 ContextMenu 样式（" + which + "）", cm != null);
                Check("下拉菜单有 MenuItem 样式（" + which + "）", mi != null);
                if (cm == null || mi == null) continue;

                var cmTpl = TemplateOf(cm);
                var miTpl = TemplateOf(mi);
                Check("ContextMenu 给了自定义模板（" + which + "）", cmTpl != null);
                Check("MenuItem 给了自定义模板（" + which + "）", miTpl != null);
                if (cmTpl == null || miTpl == null) continue;

                // 菜单项：量一个 260 宽的行，看模板能不能真渲染出内容
                var probe = new System.Windows.Controls.MenuItem
                {
                    Header = "布局样式",
                    IsCheckable = true,
                    IsChecked = true,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI, Segoe UI"),
                    FontSize = 13,
                    Padding = new Thickness(10, 7, 10, 7)
                };
                probe.Style = mi;
                probe.Measure(new Size(260, double.PositiveInfinity));
                probe.Arrange(new Rect(0, 0, 260, probe.DesiredSize.Height));
                probe.UpdateLayout();

                int rh = Math.Max(1, (int)Math.Ceiling(probe.DesiredSize.Height));
                var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    260, rh, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bmp.Render(probe);
                Check("菜单项能离屏渲染出内容（" + which + "）", NonBlack(bmp) > 20);
                Out.AppendLine("       菜单项量到高 " + probe.DesiredSize.Height.ToString("0.0")
                              + "，渲染 " + rh + "px");

                // 对勾列用**结构**断言而不是找蓝色像素：离屏渲染出来的抗锯齿色值
                // 不可靠，而且真正要保证的是「勾上时那个 Check 元素会出现」。
                var check = miTpl.FindName("Check", probe) as FrameworkElement;
                Check("模板里有对勾列元素 Check（" + which + "）", check != null);
                Check("勾选时对勾列可见（" + which + "）",
                      check != null && check.Visibility == Visibility.Visible);
                // 反过来再验一次：IsChecked=false 时它必须收起来
                probe.IsChecked = false;
                probe.UpdateLayout();
                Check("取消勾选后对勾列隐藏（" + which + "）",
                      check != null && check.Visibility == Visibility.Collapsed);
                probe.IsChecked = true;

                if (dark) SavePng(bmp, "selftest_menuitem_" + which + ".png");

                // 卡片底色：Style 本身没有 Background，得从Setter 里取 ——
                // 这条断链的症状正是「深色主题下整张下拉是一片白」
                var want = (System.Windows.Media.SolidColorBrush)
                    new System.Windows.Media.BrushConverter().ConvertFromString(Kit.Palette.Card);
                var got = SetterBrush(cm, Control.BackgroundProperty);
                Check("下拉卡片底色跟调色板一致（" + which + "）",
                      got is System.Windows.Media.SolidColorBrush gb
                      && gb.Color.ToString().Equals(want.Color.ToString()));
            }
            Kit.UseTheme(true);
        }

        private static Style FindStyle(ResourceDictionary dict, Type target)
        {
            foreach (var k in dict.Keys)
            {
                if (dict[k] is Style s && s.TargetType == target) return s;
            }
            return null;
        }

        /// <summary>
        /// 从样式里取 ControlTemplate。
        /// 走 <see cref="Setter"/> 而不是 <c>Style.Template</c>，并且注意
        /// TemplateProperty 挂在 <see cref="Control"/> 上、不在 FrameworkElement 上。
        /// </summary>
        private static System.Windows.Controls.ControlTemplate TemplateOf(Style style)
        {
            if (style == null) return null;
            foreach (var s in style.Setters)
            {
                if (s is Setter set &&
                    set.Property == Control.TemplateProperty &&
                    set.Value is System.Windows.Controls.ControlTemplate tpl) return tpl;
            }
            return null;
        }

        /// <summary>取样式里某个依赖属性的Setter 值（这里只关心画刷）。</summary>
        private static System.Windows.Media.Brush SetterBrush(Style style, DependencyProperty prop)
        {
            if (style == null) return null;
            foreach (var s in style.Setters)
            {
                if (s is Setter set && set.Property == prop) return set.Value as System.Windows.Media.Brush;
            }
            return null;
        }

        /// <summary>数一数有多少个「不是全透明也不是黑」的像素，用来判断有没有真画上东西。</summary>
        private static int NonBlack(System.Windows.Media.Imaging.BitmapSource bmp)
        {
            var px = Grab(bmp, out int w, out int h);
            int n = 0;
            for (int i = 0; i < px.Length; i += 4)
            {
                // WPF 的 Pbgra32 是预乘的，rgb 都为 0（含 alpha=0）算空白
                if (px[i] > 8 || px[i + 1] > 8 || px[i + 2] > 8) n++;
            }
            return n;
        }

        /// <summary>
        /// 有没有「偏蓝的亮像素」—— 强调色 iOS 蓝 #0A84FF / #007AFF 一类。
        /// 用来证明勾选列那个✓ 真的画出来了，而不是只有一排文字。
        /// </summary>
        private static bool HasAccentPixel(System.Windows.Media.Imaging.BitmapSource bmp)
        {
            var px = Grab(bmp, out int w, out int h);
            for (int i = 0; i < px.Length; i += 4)
            {
                int b = px[i + 2], r = px[i], g = px[i + 1];
                if (b > 150 && b - r > 60 && b - g > 25) return true;
            }
            return false;
        }

        private static byte[] Grab(System.Windows.Media.Imaging.BitmapSource bmp, out int w, out int h)
        {
            w = bmp.PixelWidth; h = bmp.PixelHeight;
            var px = new byte[Math.Max(0, w * h * 4)];
            if (px.Length > 0) bmp.CopyPixels(px, w * 4, 0);
            return px;
        }

        private static void SavePng(System.Windows.Media.Imaging.BitmapSource bmp, string name)
        {
            try
            {
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                string path = System.IO.Path.Combine(
                    AppContext.BaseDirectory, "selftest", name);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                using (var fs = System.IO.File.Create(path)) enc.Save(fs);
                Out.AppendLine("     渲染图: " + path);
            }
            catch (Exception ex)
            {
                Out.AppendLine("     渲染图保存失败: " + ex.Message);
            }
        }

        // ---- 信息层元素 ----

        private static void DrawAdofai(Adofai.AdofaiOverlay ov, Rect area)
        {
            var vis = new System.Windows.Media.DrawingVisual();
            using (var dc = vis.RenderOpen())
                ov.Draw(dc, area, new System.Windows.Media.Typeface("Segoe UI"), 1.0, 1.0);
        }

        /// <summary>
        /// 信息层的元素布局。走的是真正的绘制路径（DrawingContext 到 DrawingVisual），
        /// 再把画出来的矩形回读做命中测试与拖动 —— 和鼠标拖动时跑的是同一段代码，
        /// 但不需要模拟输入（模拟键鼠会抢用户的键盘，而且有的机器上会被 UIPI 挡掉）。
        /// </summary>
        private static void AdofaiElementLayout()
        {
            var ov = new Adofai.AdofaiOverlay();
            var s = ov.Settings;

            s.Enabled = true;
            s.AutoLayout = true;
            s.SnapAnchor = (int)KvAnchor.TopCenter;
            s.SnapMargin = 20;
            s.FontSize = 20;
            s.Sanitize();

            ov.Visible = true;
            var st = ov.State;
            st.InLevel = true;
            st.CurrentTile = 13;
            st.TotalTiles = 1242;
            st.Combo = 7;
            st.ComboTitle = Adofai.AdofaiComboTitle.PerfectPlay;
            st.HitCounts[(int)Adofai.AdofaiHitMargin.Perfect] = 5;
            st.HitCounts[(int)Adofai.AdofaiHitMargin.FailMiss] = 1;

            var area = new Rect(0, 0, 1000, 800);
            DrawAdofai(ov, area);

            Check("八个元素都画出来了（实际 " + ov.Boxes.Count + "）", ov.Boxes.Count == 8);
            Check("包围盒非空", !ov.GroupRect.IsEmpty);

            bool ordered = true;
            for (int i = 1; i < ov.Boxes.Count; i++)
                if (ov.Boxes[i].Rect.Top < ov.Boxes[i - 1].Rect.Bottom - 0.5) ordered = false;
            Check("自动排列自上而下、不重叠", ordered);

            double cx = ov.GroupRect.Left + ov.GroupRect.Width * 0.5;
            Check(string.Format("中上吸附后水平居中（中心 {0:0.#}）", cx), Math.Abs(cx - 500) < 1.0);
            Check(string.Format("中上吸附后贴顶 + 边距 20（顶 {0:0.#}）", ov.GroupRect.Top),
                Math.Abs(ov.GroupRect.Top - 20) < 1.0);

            // ---- 把判定条单独拖走 ----

            var judge = s.Find(Adofai.AdofaiElements.Judge);
            var acc = s.Find(Adofai.AdofaiElements.Acc);
            var judgeBefore = ov.RectOf(Adofai.AdofaiElements.Judge);
            var accBefore = ov.RectOf(Adofai.AdofaiElements.Acc);
            Check("判定条有矩形", !judgeBefore.IsEmpty);

            var hit = ov.HitTestElement(new Point(judgeBefore.Left + 5, judgeBefore.Top + 5));
            Check("命中判定条", ReferenceEquals(hit, judge));
            Check("命中空白处返回 null",
                ov.HitTestElement(new Point(2, area.Height - 2)) == null);

            Check("脱离自动排列成功", ov.DetachAutoLayout());
            Check("脱离后 AutoLayout = false", !s.AutoLayout);
            Check("脱离后判定条记住了自己的位置",
                judge.X > 0.01 && judge.X < 0.99 && judge.Y > 0.01 && judge.Y < 0.99);

            Check("移动判定条", ov.MoveElement(judge, 200, 300, area));
            DrawAdofai(ov, area);

            var judgeAfter = ov.RectOf(Adofai.AdofaiElements.Judge);
            var accAfter = ov.RectOf(Adofai.AdofaiElements.Acc);
            Check(string.Format("判定条位移 = 拖拽量（Δ {0:0.#},{1:0.#}）",
                    judgeAfter.Left - judgeBefore.Left, judgeAfter.Top - judgeBefore.Top),
                Math.Abs(judgeAfter.Left - judgeBefore.Left - 200) < 1.5 &&
                Math.Abs(judgeAfter.Top - judgeBefore.Top - 300) < 1.5);
            Check("其余元素原地不动",
                Math.Abs(accAfter.Left - accBefore.Left) < 1.5 &&
                Math.Abs(accAfter.Top - accBefore.Top) < 1.5);

            // 拖出屏幕要被夹回来（只夹位置，不改大小）
            ov.MoveElement(judge, 99999, 99999, area);
            DrawAdofai(ov, area);
            var clamped = ov.RectOf(Adofai.AdofaiElements.Judge);
            Check(string.Format("拖到屏幕外被夹回（右下 {0:0.#},{1:0.#}）", clamped.Right, clamped.Bottom),
                clamped.Right <= area.Width + 0.5 && clamped.Bottom <= area.Height + 0.5);

            // ---- 隐藏 ----

            s.Find(Adofai.AdofaiElements.Bpm).Visible = false;
            DrawAdofai(ov, area);
            Check("隐藏后不再出现", ov.RectOf(Adofai.AdofaiElements.Bpm).IsEmpty);
            Check("隐藏后元素数 7（实际 " + ov.Boxes.Count + "）", ov.Boxes.Count == 7);
            s.Find(Adofai.AdofaiElements.Bpm).Visible = true;
            DrawAdofai(ov, area);

            // ---- 换锚点：整块跟着走，尺寸不变 ----

            s.AutoLayout = true;
            DrawAdofai(ov, area);                 // 先回到自动排列，再量「基准尺寸」
            double w0 = ov.GroupRect.Width, h0 = ov.GroupRect.Height;

            s.SnapAnchor = (int)KvAnchor.BottomRight;
            DrawAdofai(ov, area);
            Check(string.Format("右下吸附（右下 {0:0.#},{1:0.#}）",
                    ov.GroupRect.Right, ov.GroupRect.Bottom),
                Math.Abs(ov.GroupRect.Right - 980) < 1.0 && Math.Abs(ov.GroupRect.Bottom - 780) < 1.0);
            Check("换锚点不改尺寸",
                Math.Abs(ov.GroupRect.Width - w0) < 0.5 && Math.Abs(ov.GroupRect.Height - h0) < 0.5);

            // ---- 拖动整体：吸附自动让位给手设位置 ----

            Check("整块平移", ov.MoveGroup(-100, -50, area));
            Check("拖整体后吸附自动关闭", s.SnapAnchor == (int)KvAnchor.Free);
            DrawAdofai(ov, area);
            Check(string.Format("整块位移正确（Δ {0:0.#},{1:0.#}）",
                    ov.GroupRect.Right - 980, ov.GroupRect.Bottom - 780),
                Math.Abs(ov.GroupRect.Right - 880) < 1.5 && Math.Abs(ov.GroupRect.Bottom - 730) < 1.5);
        }

        private static void Report(string what, int expect, int got)
        {
            bool ok = expect == got;
            if (ok) _pass++; else _fail++;
            Out.AppendLine(string.Format("  {0}  {1,-46} expect={2,-4} got={3}",
                ok ? "OK  " : "FAIL", what, expect, got));
        }

        private static HashSet<int> Keys(params int[] vks) => new HashSet<int>(vks);
    }
}

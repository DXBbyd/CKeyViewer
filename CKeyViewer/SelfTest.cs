using System;
using System.Collections.Generic;
using System.Text;
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

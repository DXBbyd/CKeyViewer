using System;
using System.Collections.Generic;

namespace CKeyViewer.Core
{
    /// <summary>
    /// Unity KeyCode ↔ Win32 虚拟键码 ↔ 显示名 的映射。
    /// 原版配置里存的是 Unity KeyCode（例如 97='A'、282=F1、304=LeftShift）。
    /// </summary>
    public static class KeyCodeMap
    {
        // ---- 特殊 Unity KeyCode ----
        public const int UnityBackspace = 8;
        public const int UnityTab = 9;
        public const int UnityReturn = 13;
        public const int UnityEscape = 27;
        public const int UnitySpace = 32;
        public const int UnityDelete = 127;
        public const int UnityKeypad0 = 256;
        public const int UnityKeypadEnter = 271;
        public const int UnityUpArrow = 273;
        public const int UnityDownArrow = 274;
        public const int UnityRightArrow = 275;
        public const int UnityLeftArrow = 276;
        public const int UnityInsert = 277;
        public const int UnityHome = 278;
        public const int UnityEnd = 279;
        public const int UnityPageUp = 280;
        public const int UnityPageDown = 281;
        public const int UnityF1 = 282;
        public const int UnityF15 = 296;
        public const int UnityNumlock = 300;
        public const int UnityCapsLock = 301;
        public const int UnityScrollLock = 302;
        public const int UnityRightShift = 303;
        public const int UnityLeftShift = 304;
        public const int UnityRightControl = 305;
        public const int UnityLeftControl = 306;
        public const int UnityRightAlt = 307;
        public const int UnityLeftAlt = 308;
        public const int UnityRightCommand = 309;
        public const int UnityLeftCommand = 310;
        public const int UnityLeftWindows = 311;
        public const int UnityRightWindows = 312;
        public const int UnityAltGr = 313;
        public const int UnityHelp = 315;
        public const int UnityPrint = 316;
        public const int UnitySysReq = 317;
        public const int UnityBreak = 318;
        public const int UnityMenu = 319;
        public const int UnityMouse0 = 323;   // 左键

        // ---- Win32 VK ----
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_CANCEL = 0x03;
        private const int VK_MBUTTON = 0x04;
        private const int VK_XBUTTON1 = 0x05;
        private const int VK_XBUTTON2 = 0x06;
        private const int VK_BACK = 0x08;
        private const int VK_TAB = 0x09;
        private const int VK_RETURN = 0x0D;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;    // Alt
        private const int VK_CAPITAL = 0x14;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_SPACE = 0x20;
        private const int VK_PRIOR = 0x21;
        private const int VK_NEXT = 0x22;
        private const int VK_END = 0x23;
        private const int VK_HOME = 0x24;
        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_DOWN = 0x28;
        private const int VK_SNAPSHOT = 0x2C;
        private const int VK_INSERT = 0x2D;
        private const int VK_DELETE = 0x2E;
        private const int VK_HELP = 0x2F;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_APPS = 0x5D;
        private const int VK_NUMPAD0 = 0x60;
        private const int VK_MULTIPLY = 0x6A;
        private const int VK_ADD = 0x6B;
        private const int VK_SUBTRACT = 0x6D;
        private const int VK_DECIMAL = 0x6E;
        private const int VK_DIVIDE = 0x6F;
        private const int VK_F1 = 0x70;
        private const int VK_NUMLOCK = 0x90;
        private const int VK_SCROLL = 0x91;
        private const int VK_OEM_NEC_EQUAL = 0x92;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const int VK_OEM_1 = 0xBA;      // ;:
        private const int VK_OEM_PLUS = 0xBB;   // =+
        private const int VK_OEM_COMMA = 0xBC;  // ,<
        private const int VK_OEM_MINUS = 0xBD;  // -_
        private const int VK_OEM_PERIOD = 0xBE; // .>
        private const int VK_OEM_2 = 0xBF;      // /?
        private const int VK_OEM_3 = 0xC0;      // `~
        private const int VK_OEM_4 = 0xDB;      // [{
        private const int VK_OEM_5 = 0xDC;      // \|
        private const int VK_OEM_6 = 0xDD;      // ]}
        private const int VK_OEM_7 = 0xDE;      // '"

        private static readonly Dictionary<int, int> ToVk = BuildToVk();
        private static readonly Dictionary<int, int> ToUnity = BuildToUnity();

        private static Dictionary<int, int> BuildToVk()
        {
            var d = new Dictionary<int, int>();

            // 字母：Unity 用小写 ASCII 97..122
            for (int i = 0; i < 26; i++) d[97 + i] = 0x41 + i;
            // 数字 0..9
            for (int i = 0; i < 10; i++) d[48 + i] = 0x30 + i;
            // 小键盘 0..9
            for (int i = 0; i < 10; i++) d[UnityKeypad0 + i] = VK_NUMPAD0 + i;
            // 功能键 F1..F15
            for (int i = 0; i <= (UnityF15 - UnityF1); i++) d[UnityF1 + i] = VK_F1 + i;

            d[UnityBackspace] = VK_BACK;
            d[UnityTab] = VK_TAB;
            d[UnityReturn] = VK_RETURN;
            d[UnityEscape] = VK_ESCAPE;
            d[UnitySpace] = VK_SPACE;
            d[UnityDelete] = VK_DELETE;

            d[266] = VK_DECIMAL;        // KeypadPeriod
            d[267] = VK_DIVIDE;         // KeypadDivide
            d[268] = VK_MULTIPLY;       // KeypadMultiply
            d[269] = VK_SUBTRACT;       // KeypadMinus
            d[270] = VK_ADD;            // KeypadPlus
            d[UnityKeypadEnter] = VK_RETURN;
            d[272] = VK_OEM_NEC_EQUAL;  // KeypadEquals

            d[UnityUpArrow] = VK_UP;
            d[UnityDownArrow] = VK_DOWN;
            d[UnityRightArrow] = VK_RIGHT;
            d[UnityLeftArrow] = VK_LEFT;

            d[UnityInsert] = VK_INSERT;
            d[UnityHome] = VK_HOME;
            d[UnityEnd] = VK_END;
            d[UnityPageUp] = VK_PRIOR;
            d[UnityPageDown] = VK_NEXT;

            d[UnityNumlock] = VK_NUMLOCK;
            d[UnityCapsLock] = VK_CAPITAL;
            d[UnityScrollLock] = VK_SCROLL;
            d[UnityRightShift] = VK_RSHIFT;
            d[UnityLeftShift] = VK_LSHIFT;
            d[UnityRightControl] = VK_RCONTROL;
            d[UnityLeftControl] = VK_LCONTROL;
            d[UnityRightAlt] = VK_RMENU;
            d[UnityLeftAlt] = VK_LMENU;
            d[UnityRightCommand] = VK_RWIN;
            d[UnityLeftCommand] = VK_LWIN;
            d[UnityLeftWindows] = VK_LWIN;
            d[UnityRightWindows] = VK_RWIN;
            d[UnityAltGr] = VK_RMENU;
            d[UnityHelp] = VK_HELP;
            d[UnityPrint] = VK_SNAPSHOT;
            d[UnitySysReq] = VK_SNAPSHOT;
            d[UnityBreak] = VK_CANCEL;
            d[UnityMenu] = VK_APPS;

            d[UnityMouse0] = VK_LBUTTON;
            d[UnityMouse0 + 1] = VK_RBUTTON;
            d[UnityMouse0 + 2] = VK_MBUTTON;
            d[UnityMouse0 + 3] = VK_XBUTTON1;
            d[UnityMouse0 + 4] = VK_XBUTTON2;

            // 标点
            d[39] = VK_OEM_7;    // '
            d[44] = VK_OEM_COMMA;
            d[45] = VK_OEM_MINUS;
            d[46] = VK_OEM_PERIOD;
            d[47] = VK_OEM_2;
            d[59] = VK_OEM_1;
            d[61] = VK_OEM_PLUS;
            d[91] = VK_OEM_4;
            d[92] = VK_OEM_5;
            d[93] = VK_OEM_6;
            d[96] = VK_OEM_3;

            return d;
        }

        private static Dictionary<int, int> BuildToUnity()
        {
            var d = new Dictionary<int, int>();
            foreach (var kv in BuildToVk())
            {
                // 反向映射：优先保留第一个（避免 Left/Right 修饰键互相覆盖）
                if (!d.ContainsKey(kv.Value)) d[kv.Value] = kv.Key;
            }
            // 确保左右修饰键反向映射正确
            d[VK_LSHIFT] = UnityLeftShift;
            d[VK_RSHIFT] = UnityRightShift;
            d[VK_LCONTROL] = UnityLeftControl;
            d[VK_RCONTROL] = UnityRightControl;
            d[VK_LMENU] = UnityLeftAlt;
            d[VK_RMENU] = UnityRightAlt;
            d[VK_LBUTTON] = UnityMouse0;
            d[VK_RBUTTON] = UnityMouse0 + 1;
            d[VK_MBUTTON] = UnityMouse0 + 2;
            return d;
        }

        /// <summary>Unity KeyCode → Win32 虚拟键码；未知返回 0。</summary>
        public static int ToVirtualKey(int unityKeyCode)
        {
            return ToVk.TryGetValue(unityKeyCode, out int vk) ? vk : 0;
        }

        /// <summary>Win32 虚拟键码 → Unity KeyCode；未知返回 0。</summary>
        public static int FromVirtualKey(int vk)
        {
            return ToUnity.TryGetValue(vk, out int u) ? u : 0;
        }

        // ---- Unity KeyCode 枚举名 ----
        //
        // 自由布局节点的 KeyBind 存的是 **Unity 枚举成员名**（原版用
        // Enum.TryParse<KeyCode>(node.KeyBind, true) 解析），跟键帽上显示的
        // 标签（DisplayName，如 "Back"/"PrtSc"）不是一回事，所以单独建表。

        private static readonly Dictionary<int, string> UnityEnumNames = BuildUnityEnumNames();
        private static readonly Dictionary<string, int> UnityEnumByLower = BuildUnityEnumIndex();

        private static Dictionary<int, string> BuildUnityEnumNames()
        {
            var d = new Dictionary<int, string>
            {
                [8] = "Backspace", [9] = "Tab", [12] = "Clear", [13] = "Return",
                [19] = "Pause", [27] = "Escape", [32] = "Space", [127] = "Delete",

                [39] = "Quote", [44] = "Comma", [45] = "Minus", [46] = "Period",
                [47] = "Slash", [59] = "Semicolon", [61] = "Equals",
                [91] = "LeftBracket", [92] = "Backslash", [93] = "RightBracket", [96] = "BackQuote",

                [266] = "KeypadPeriod", [267] = "KeypadDivide", [268] = "KeypadMultiply",
                [269] = "KeypadMinus", [270] = "KeypadPlus", [271] = "KeypadEnter",
                [272] = "KeypadEquals",

                [273] = "UpArrow", [274] = "DownArrow", [275] = "RightArrow", [276] = "LeftArrow",
                [277] = "Insert", [278] = "Home", [279] = "End", [280] = "PageUp", [281] = "PageDown",

                [300] = "Numlock", [301] = "CapsLock", [302] = "ScrollLock",
                [303] = "RightShift", [304] = "LeftShift",
                [305] = "RightControl", [306] = "LeftControl",
                [307] = "RightAlt", [308] = "LeftAlt",
                [309] = "RightCommand", [310] = "LeftCommand",
                [311] = "LeftWindows", [312] = "RightWindows",
                [313] = "AltGr", [315] = "Help", [316] = "Print", [317] = "SysReq",
                [318] = "Break", [319] = "Menu",

                [323] = "Mouse0", [324] = "Mouse1", [325] = "Mouse2",
                [326] = "Mouse3", [327] = "Mouse4", [328] = "Mouse5", [329] = "Mouse6",
            };

            for (int i = 0; i < 26; i++) d[97 + i] = ((char)('A' + i)).ToString();
            for (int i = 0; i < 10; i++) d[48 + i] = "Alpha" + i;
            for (int i = 0; i < 10; i++) d[UnityKeypad0 + i] = "Keypad" + i;
            for (int i = 0; i <= (UnityF15 - UnityF1); i++) d[UnityF1 + i] = "F" + (i + 1);

            return d;
        }

        private static Dictionary<string, int> BuildUnityEnumIndex()
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in UnityEnumNames) d[kv.Value] = kv.Key;

            // Unity 里这几个是同值的别名，两个名字都要能解析
            d["RightApple"] = 309;
            d["LeftApple"] = 310;
            d["None"] = 0;
            return d;
        }

        /// <summary>Unity KeyCode → 枚举成员名（写进配置的 KeyBind 就是它）；未知返回空串。</summary>
        public static string NameOf(int unityKeyCode)
            => UnityEnumNames.TryGetValue(unityKeyCode, out string n) ? n : "";

        /// <summary>枚举成员名 → Unity KeyCode；大小写不敏感，未知返回 0。</summary>
        public static int FromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            return UnityEnumByLower.TryGetValue(name.Trim(), out int c) ? c : 0;
        }

        // ---- 显示名 ----

        private static readonly Dictionary<int, string> SpecialNames = new Dictionary<int, string>
        {
            { UnityBackspace, "Back" }, { UnityTab, "Tab" }, { UnityReturn, "Enter" },
            { UnityEscape, "Esc" }, { UnitySpace, "Space" }, { UnityDelete, "Del" },
            { 266, "." }, { 267, "/" }, { 268, "*" }, { 269, "-" }, { 270, "+" },
            { UnityKeypadEnter, "NumpadEnt" }, { 272, "=" },
            { UnityUpArrow, "Up" }, { UnityDownArrow, "Down" },
            { UnityRightArrow, "Right" }, { UnityLeftArrow, "Left" },
            { UnityInsert, "Ins" }, { UnityHome, "Home" }, { UnityEnd, "End" },
            { UnityPageUp, "PgUp" }, { UnityPageDown, "PgDn" },
            { UnityNumlock, "NumLk" }, { UnityCapsLock, "Caps" }, { UnityScrollLock, "Scrlk" },
            { UnityRightShift, "RShift" }, { UnityLeftShift, "Shift" },
            { UnityRightControl, "RCtrl" }, { UnityLeftControl, "Ctrl" },
            { UnityRightAlt, "RAlt" }, { UnityLeftAlt, "Alt" },
            { UnityRightCommand, "RCmd" }, { UnityLeftCommand, "Cmd" },
            { UnityLeftWindows, "LWin" }, { UnityRightWindows, "RWin" },
            { UnityAltGr, "AltGr" }, { UnityHelp, "Help" },
            { UnityPrint, "PrtSc" }, { UnitySysReq, "SysRq" }, { UnityBreak, "Pause" },
            { UnityMenu, "Menu" },
            { UnityMouse0, "M1" }, { UnityMouse0 + 1, "M2" }, { UnityMouse0 + 2, "M3" },
            { UnityMouse0 + 3, "M4" }, { UnityMouse0 + 4, "M5" },
        };

        /// <summary>取得键帽上显示的标签。</summary>
        public static string DisplayName(int unityKeyCode)
        {
            if (unityKeyCode == 0) return "";
            if (SpecialNames.TryGetValue(unityKeyCode, out string name)) return name;

            if (unityKeyCode >= UnityKeypad0 && unityKeyCode <= UnityKeypad0 + 9)
                return "N" + (unityKeyCode - UnityKeypad0);

            if (unityKeyCode >= UnityF1 && unityKeyCode <= UnityF15)
                return "F" + (unityKeyCode - UnityF1 + 1);

            if (unityKeyCode >= 97 && unityKeyCode <= 122)
                return ((char)(unityKeyCode - 32)).ToString();

            if (unityKeyCode >= 48 && unityKeyCode <= 57)
                return ((char)unityKeyCode).ToString();

            if (unityKeyCode >= 33 && unityKeyCode <= 126)
                return ((char)unityKeyCode).ToString();

            return "K" + unityKeyCode;
        }
    }
}

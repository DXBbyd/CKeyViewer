using System;
using System.Windows;
using System.Windows.Interop;
using CKeyViewer.Core;
using CKeyViewer.Native;

namespace CKeyViewer
{
    /// <summary>
    /// 全局热键。注册在覆盖层窗口上 —— 该窗口虽为鼠标穿透且不抢焦点，
    /// 但依然有消息队列，能够正常接收 <c>WM_HOTKEY</c>。
    /// </summary>
    public sealed class KvHotkeys : IDisposable
    {
        private const int IdToggle = 0x4B01;
        private const int IdReset = 0x4B02;
        private const int IdNextProfile = 0x4B03;
        private const int IdSettings = 0x4B04;
        private const int IdLayout = 0x4B05;

        public const uint ModCtrlAlt = Win32.MOD_CONTROL | Win32.MOD_ALT;

        private readonly Window _window;
        private IntPtr _hwnd;
        private HwndSource _source;
        private bool _hooked;
        private bool _attached;

        /// <summary>Ctrl+Alt+K —— 显示 / 隐藏覆盖层。</summary>
        public event Action Toggle;

        /// <summary>Ctrl+Alt+R —— 计数归零。</summary>
        public event Action Reset;

        /// <summary>Ctrl+Alt+N（被占用时退到 Ctrl+Alt+P / Ctrl+Alt+F11）—— 切到下一个档案。</summary>
        public event Action NextProfile;

        /// <summary>Ctrl+Alt+S —— 打开设置窗口。</summary>
        public event Action Settings;

        /// <summary>Ctrl+Alt+L（被占用时退到 Ctrl+Alt+F8）—— 切换「自由布局」拖动模式。</summary>
        public event Action ToggleLayout;

        /// <summary>每个热键是否注册成功（供设置界面展示）。</summary>
        public bool ToggleRegistered { get; private set; }
        public bool ResetRegistered { get; private set; }
        public bool NextProfileRegistered { get; private set; }
        public bool SettingsRegistered { get; private set; }
        public bool LayoutRegistered { get; private set; }

        public KvHotkeys(Window window)
        {
            _window = window;
            Current = this;
        }

        /// <summary>当前实例 —— 设置面板需要它来显示实际生效的热键组合。</summary>
        public static KvHotkeys Current { get; private set; }

        /// <summary>
        /// 真正把热键挂上去。必须在窗口 <c>Show()</c> 之后调用 ——
        /// WPF 的 HWND 是延迟创建的，Show 之前 <c>Handle</c> 恒为 0，
        /// 这时候 RegisterHotKey 会全部失败。
        /// </summary>
        public void Attach()
        {
            if (_attached) return;
            _attached = true;

            try
            {
                _hwnd = Win32.Handle(_window);
                if (_hwnd == IntPtr.Zero)
                {
                    // 兜底：等 SourceInitialized
                    _window.SourceInitialized += (s, e) => AttachCore();
                    return;
                }
                AttachCore();
            }
            catch (Exception ex)
            {
                Diag.Log("hotkeys attach: " + ex.Message);
            }
        }

        private void AttachCore()
        {
            if (_hwnd == IntPtr.Zero) _hwnd = Win32.Handle(_window);

            if (_hwnd != IntPtr.Zero && !_hooked)
            {
                _source = HwndSource.FromHwnd(_hwnd);
                if (_source != null)
                {
                    _source.AddHook(WndProc);
                    _hooked = true;
                }
            }
            RegisterAll();
        }

        private void RegisterAll()
        {
            // 每个动作给一个「主选 + 备选」的按键序列：
            // Ctrl+Alt+字母 很容易和别的软件撞车（实测 Ctrl+Alt+P 在本机就被占用），
            // 撞了就自动退到功能键，保证一定有一组可用。
            ToggleRegistered = Register(IdToggle, 0x4B, 0x78);            // K → F9
            ResetRegistered = Register(IdReset, 0x52, 0x79);              // R → F10
            NextProfileRegistered = Register(IdNextProfile, 0x4E, 0x50, 0x7A); // N → P → F11
            SettingsRegistered = Register(IdSettings, 0x53, 0x7B);        // S → F12
            LayoutRegistered = Register(IdLayout, 0x4C, 0x77);            // L → F8

            Diag.Log(string.Format("hotkeys K={0} R={1} P={2} S={3} L={4} | {5} / {6} / {7} / {8} / {9}",
                ToggleRegistered, ResetRegistered, NextProfileRegistered, SettingsRegistered, LayoutRegistered,
                ToggleKeyName, ResetKeyName, NextProfileKeyName, SettingsKeyName, LayoutKeyName));
        }

        /// <summary>实际生效的热键描述（供设置面板显示）。</summary>
        public string ToggleKeyName { get; private set; } = "未注册";
        public string ResetKeyName { get; private set; } = "未注册";
        public string NextProfileKeyName { get; private set; } = "未注册";
        public string SettingsKeyName { get; private set; } = "未注册";
        public string LayoutKeyName { get; private set; } = "未注册";

        private bool Register(int id, params uint[] vks)
        {
            if (_hwnd == IntPtr.Zero) return false;

            for (int i = 0; i < vks.Length; i++)
            {
                try
                {
                    if (!Win32.RegisterHotKeySafe(_hwnd, id, ModCtrlAlt, vks[i])) continue;
                    SetName(id, "Ctrl+Alt+" + VkName(vks[i]));
                    return true;
                }
                catch (Exception ex)
                {
                    Diag.Log("RegisterHotKey failed: " + ex.Message);
                }
            }
            SetName(id, "未注册（被其它程序占用）");
            return false;
        }

        private void SetName(int id, string name)
        {
            switch (id)
            {
                case IdToggle: ToggleKeyName = name; break;
                case IdReset: ResetKeyName = name; break;
                case IdNextProfile: NextProfileKeyName = name; break;
                case IdSettings: SettingsKeyName = name; break;
                case IdLayout: LayoutKeyName = name; break;
            }
        }

        private static string VkName(uint vk)
        {
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x70 && vk <= 0x7B) return "F" + (vk - 0x6F);
            switch (vk)
            {
                case 0x25: return "←";
                case 0x26: return "↑";
                case 0x27: return "→";
                case 0x28: return "↓";
                default: return "0x" + vk.ToString("X2");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                Diag.Log("WM_HOTKEY id=0x" + id.ToString("X4"));
                switch (id)
                {
                    case IdToggle:
                        handled = true;
                        Toggle?.Invoke();
                        break;
                    case IdReset:
                        handled = true;
                        Reset?.Invoke();
                        break;
                    case IdNextProfile:
                        handled = true;
                        NextProfile?.Invoke();
                        break;
                    case IdSettings:
                        handled = true;
                        Settings?.Invoke();
                        break;
                    case IdLayout:
                        handled = true;
                        ToggleLayout?.Invoke();
                        break;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hooked && _source != null)
            {
                _source.RemoveHook(WndProc);
                _hooked = false;
            }

            if (_hwnd != IntPtr.Zero)
            {
                foreach (int id in new[] { IdToggle, IdReset, IdNextProfile, IdSettings, IdLayout })
                {
                    try { Win32.UnregisterHotKeySafe(_hwnd, id); } catch { }
                }
            }
        }
    }
}

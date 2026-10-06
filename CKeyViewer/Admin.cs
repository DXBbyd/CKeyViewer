using System;
using System.Security.Principal;

namespace CKeyViewer
{
    /// <summary>
    /// 管理员权限检查。
    ///
    /// 为什么用运行时检查 + 自定义弹窗，而不是在清单里写
    /// <c>requestedExecutionLevel = requireAdministrator</c>：
    /// 清单方式弹的是 Windows 自己的 UAC 对话框，用户看不到我们想说的话；
    /// 而且被拒绝时是系统直接结束进程，没有机会告诉用户「该怎么办」。
    ///
    /// 之所以要管理员权限：按键状态走 <c>GetAsyncKeyState</c>，
    /// 当以更高完整性级别运行的程序（例如管理员启动的游戏）在前台时，
    /// 低完整性级别的进程读不到它的按键 —— 表现为按键完全没反应。
    /// </summary>
    internal static class Admin
    {
        /// <summary>提示文案（按需求固定）。</summary>
        public const string RequireMessage = "请使用管理员运行此程序";

        /// <summary>当前进程是否以管理员身份（高完整性级别）运行。</summary>
        public static bool IsElevated()
        {
#if DEBUG
            // 只有 Debug 构建认这个开关 —— 开发机上未必有管理员权限，
            // 调 UI、取截图、跑布局模式验证时都得靠它。
            // Release 构建里整段被编译掉（连字符串都不会留下），发布的程序严格强制管理员。
            if (Environment.GetEnvironmentVariable("CKV_SKIP_ADMIN") == "1")
            {
                Core.Diag.Log("admin check skipped (CKV_SKIP_ADMIN=1, debug build only)");
                return true;
            }
#endif
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception ex)
            {
                Core.Diag.Log("admin check failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 不是管理员就弹窗告知；返回 false 表示调用方应当结束进程。
        /// 弹窗走 Win32 的 <c>MessageBoxW</c> 并带 <c>MB_TOPMOST</c> ——
        /// 本程序有全屏置顶覆盖层，普通弹窗会被它压在下面看不见。
        /// </summary>
        public static bool EnsureElevated()
        {
            if (IsElevated()) return true;

            try
            {
                Native.Win32.Alert(RequireMessage, "CKeyViewer");
            }
            catch (Exception ex)
            {
                Core.Diag.Log("alert failed: " + ex.Message);
            }
            return false;
        }
    }
}

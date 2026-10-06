using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace CKeyViewer
{
    /// <summary>
    /// 作者信息、版本号与头像 —— 界面各处（设置面板「关于」页、托盘菜单、关于对话框）
    /// 共用这一份常量，不再各自硬编码字符串。
    /// </summary>
    internal static class About
    {
        public const string ProductName = "CKeyViewer";
        public const string Author = "DXBbyd";
        public const string QQ = "3157037483";

        public const string RepoUrl = "https://github.com/DXBbyd/CKeyViewer";
        public const string ReleasesUrl = RepoUrl + "/releases/latest";

        /// <summary>配置格式参照的上游项目（原版是 Unity 游戏 Mod）。</summary>
        public const string UpstreamName = "JipperKeyViewer";
        public const string UpstreamAuthor = "HitMargin";
        public const string UpstreamUrl = "https://github.com/adofaiex/JipperKeyViewer";

        private const string AvatarResource = "CKeyViewer.avatar.png";

        /// <summary>形如 <c>1.0.0</c>（丢掉 AssemblyVersion 的第 4 位）。</summary>
        public static string Version
        {
            get
            {
                try
                {
                    Version v = Assembly.GetExecutingAssembly().GetName().Version;
                    if (v == null) return "1.0.0";
                    return string.Format("{0}.{1}.{2}", v.Major, v.Minor, v.Build);
                }
                catch { return "1.0.0"; }
            }
        }

        /// <summary>形如 <c>CKeyViewer 1.0.0</c>，标题栏与托盘悬浮提示都用它。</summary>
        public static string TitleWithVersion
        {
            get { return ProductName + " " + Version; }
        }

        private static BitmapImage _avatar;
        private static bool _avatarTried;

        /// <summary>
        /// 读程序集里嵌入的作者头像。失败返回 null（界面会自动退回纯色占位块）。
        /// 结果缓存 —— 「关于」页每次重进都会调，没必要反复解码。
        /// </summary>
        public static BitmapImage LoadAvatar()
        {
            if (_avatarTried) return _avatar;
            _avatarTried = true;

            try
            {
                using (Stream s = typeof(About).Assembly.GetManifestResourceStream(AvatarResource))
                {
                    if (s == null) return null;
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;   // 立刻解码，之后就能关流
                    bmp.StreamSource = s;
                    bmp.EndInit();
                    bmp.Freeze();
                    _avatar = bmp;
                }
            }
            catch (Exception ex) { Core.Diag.Log("avatar load failed: " + ex.Message); }

            return _avatar;
        }

        /// <summary>用默认浏览器打开链接。</summary>
        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) { Core.Diag.Log("open url failed: " + ex.Message); }
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CKeyViewer.Setup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            SetupOptions opts = SetupOptions.Parse(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); }
            catch { /* 老系统上不支持就算了 */ }

            // 安装程序崩了不能是「双击之后什么都没发生」—— 留下日志 + 弹一句人话。
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            { Fail("界面异常", e.Exception, !opts.Silent); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            { Fail("未处理异常", e.ExceptionObject as Exception, !opts.Silent); };

            // 静默：无界面直接干活，适合脚本 / 批量部署
            if (opts.Silent)
            {
                if (opts.Uninstall) return Uninstaller.Begin(opts.KeepConfig, null);

                int rc = Installer.Run(opts, null);
                if (rc == 0 && opts.Launch)
                    Processes.Launch(Path.Combine(opts.Dir, AppInfo.AppExeName));
                return rc;
            }

            using (var form = new MainForm(opts))
                Application.Run(form);

            return Environment.ExitCode;
        }

        private static void Fail(string what, Exception ex, bool showUi)
        {
            string log = BuildLog(what, ex);
            string path = null;
            try
            {
                path = Path.Combine(AppInfo.SetupDir, "ckv_setup_error.log");
                File.AppendAllText(path, log, new UTF8Encoding(false));
            }
            catch
            {
                try
                {
                    path = Path.Combine(Path.GetTempPath(), "ckv_setup_error.log");
                    File.AppendAllText(path, log, new UTF8Encoding(false));
                }
                catch { }
            }

            if (!showUi) return;
            try
            {
                MessageBox.Show(
                    "安装程序遇到了一个错误：\n\n" + (ex != null ? ex.Message : what) +
                    (path != null ? "\n\n详细信息已写入：\n" + path : ""),
                    AppInfo.ProductName + " 安装程序",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        private static string BuildLog(string what, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + what + " ===");
            sb.AppendLine("版本 " + AppInfo.Version);
            sb.AppendLine("参数 " + string.Join(" ", Environment.GetCommandLineArgs()));
            sb.AppendLine(ex != null ? ex.ToString() : "(无异常对象)");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}

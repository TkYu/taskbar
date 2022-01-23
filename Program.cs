using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace taskbar
{
    static class Program
    {
        static readonly NotifyIcon _notifyIcon = new();
        private static readonly Job _jobs = new();
        private static Process _process;

        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length < 2 || !File.Exists(args[0]))
            {
                MessageBox.Show(@"Usage: taskbar.exe fileFullPath [arguments] 
Example: taskbar.exe D:\tools\ss-local.exe -c D:\tools\ss-config.json", "Usage", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.ExitCode = -1;
                return;
            }

            var exe = args[0];
            var arg = string.Join(" ",args.Skip(1));
            var selfPath = new Uri(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath;
            var dir = (exe.Contains("\\") ? Path.GetDirectoryName(exe) : Path.GetDirectoryName(selfPath))??Environment.CurrentDirectory;
            var logFile = Path.Combine(Path.GetTempPath(),$"taskbar_{Path.GetFileNameWithoutExtension(exe)}.out.log");
            var errFile = Path.Combine(Path.GetTempPath(), $"taskbar_{Path.GetFileNameWithoutExtension(exe)}.err.log");
            _process = new Process
            {
                StartInfo =
                {
                    FileName = exe,
                    Arguments = arg,
                    WorkingDirectory = dir,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            _process.Start();
            _jobs.AddProcess(_process.Handle);

            var cts = new CancellationTokenSource();
            Task.Run(() =>
            {
                using var fs = new FileStream(logFile, FileMode.Append);
                using var sw = new StreamWriter(fs) { AutoFlush = true };
                using var sro = _process.StandardOutput;
                while (!cts.IsCancellationRequested)
                {
                    var textLine = sro.ReadLine();
                    if (textLine == null)
                        break;
                    sw.WriteLine(textLine);
                }
            }, cts.Token);
            Task.Run(() =>
            {
                using var fs = new FileStream(errFile, FileMode.Append);
                using var sw = new StreamWriter(fs) { AutoFlush = true };
                using var sre = _process.StandardError;
                while (!cts.IsCancellationRequested)
                {
                    var textLine = sre.ReadLine();
                    if (textLine == null)
                        break;
                    sw.WriteLine(textLine);
                }
            }, cts.Token);

            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(selfPath);
            _notifyIcon.Visible = true;
            //_notifyIcon.Text = Application.ProductName;
            _notifyIcon.Text = Path.GetFileNameWithoutExtension(exe);

            var contextMenu = new ContextMenuStrip();
            frmLogs formLog = null;
            contextMenu.Items.Add("Show Output Log", null, (s, e) =>
            {
                if(formLog == null)
                {
                    formLog = new frmLogs(logFile);
                    formLog?.ShowDialog();
                    formLog = null;
                }
                else
                {
                    formLog.Activate();
                }
            });
            var itemShowErr = contextMenu.Items.Add("Show Error Log", null, (s, e) =>
            {
                if (formLog == null)
                {
                    formLog = new frmLogs(errFile);
                    formLog?.ShowDialog();
                    formLog = null;
                }
                else
                {
                    formLog.Activate();
                }
            });
            var itemRunStart = contextMenu.Items.Add("Run At Start", null, (s, e) =>
            {
                var item = (ToolStripMenuItem) s;
                SetStartup(!item.Checked);
                item.Checked = CheckStartup();
            });
            ((ToolStripMenuItem)itemRunStart).Checked = CheckStartup();
            contextMenu.Items.Add("Exit", null, (s, e) => { cts.Cancel(); _process.Kill(); Application.Exit(); });
            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) =>
            {
                itemShowErr.PerformClick();
            };
            Application.Run();

            _notifyIcon.Visible = false;
        }



        private static bool CheckStartup()
        {
            try
            {
                var cmd = Path.GetFullPath(Environment.GetCommandLineArgs()[0]) + " " + string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
                var kNameWithHash = $"TaskBar{cmd.GetHashCode()}";
                var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (runKey == null) return false;
                string pName = runKey.GetValue(kNameWithHash)?.ToString();
                runKey.Close();
                if (pName != null && pName.Equals(cmd, StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }
            catch
            {
                //TODO
                return false;
            }
        }

        private static bool SetStartup(bool enabled)
        {
            try
            {
                var cmd = Path.GetFullPath(Environment.GetCommandLineArgs()[0]) + " " + string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
                var kNameWithHash = $"TaskBar{cmd.GetHashCode()}";
                var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (runKey == null) return false;
                if (enabled)
                    runKey.SetValue(kNameWithHash, cmd);
                else
                    runKey.DeleteValue(kNameWithHash);
                runKey.Close();
                return true;
            }
            catch
            {
                //TODO
                return false;
            }
        }
    }
}

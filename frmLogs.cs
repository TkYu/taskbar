using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace taskbar
{
    public sealed partial class frmLogs : Form
    {
        long lastOffset;
        string filename;
        System.Windows.Forms.Timer timer;
        const int BACK_OFFSET = 65536;

        public frmLogs(string logfile)
        {
            InitializeComponent();
            Text = logfile;
            if(!File.Exists(logfile))File.WriteAllText(logfile, "");
            filename = logfile;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 500;
            timer.Tick += Timer_Tick;
            timer.Start();
        }


        private static IEnumerable<string> Tail(string file)
        {
            using (var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
            {
                if (reader.BaseStream.Length > 1024)
                {
                    reader.BaseStream.Seek(-1024, SeekOrigin.End);
                }
                while (true)
                {
                    string line = reader.ReadLine();
                    if (reader.BaseStream.Length < reader.BaseStream.Position)
                        reader.BaseStream.Seek(0, SeekOrigin.Begin);

                    if (line != null) yield return line;
                    else Thread.Sleep(500);
                }
            }
            // ReSharper disable once IteratorNeverReturns
        }

        private void frmLogs_Load(object sender, EventArgs e)
        {
            InitContent();
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 300;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateContent();
        }


        private void InitContent()
        {
            using (StreamReader reader = new StreamReader(new FileStream(filename,
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                if (reader.BaseStream.Length > BACK_OFFSET)
                {
                    reader.BaseStream.Seek(-BACK_OFFSET, SeekOrigin.End);
                    reader.ReadLine();
                }

                string line = "";
                while ((line = reader.ReadLine()) != null)
                    txtLog.AppendText(line + "\r\n");

                txtLog.ScrollToCaret();

                lastOffset = reader.BaseStream.Position;
            }
        }

        private void UpdateContent()
        {
            using (StreamReader reader = new StreamReader(new FileStream(filename,
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                reader.BaseStream.Seek(lastOffset, SeekOrigin.Begin);

                string line = "";
                bool changed = false;
                while ((line = reader.ReadLine()) != null)
                {
                    changed = true;
                    txtLog.AppendText(line + "\r\n");
                }

                if (changed)
                {
                    txtLog.ScrollToCaret();
                }

                lastOffset = reader.BaseStream.Position;
            }
        }

        private void frmLogs_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer.Stop();
        }
    }
}

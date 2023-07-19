﻿using System;
using System.Windows.Forms;

using SakuraLibrary;
using SakuraLibrary.Model;
using SakuraLibrary.Proto;
using SakuraLibrary.Helper;
using static SakuraLibrary.Proto.Log.Types;

namespace LegacyLauncher.Model
{
    public class LauncherViewModel : LauncherModel
    {
        public readonly Func<string, bool> SimpleConfirmHandler;
        public readonly Func<string, bool> SimpleWarningHandler;
        public readonly Action<bool, string> SimpleHandler;
        public readonly Action<bool, string> SimpleFailureHandler;

        public readonly MainForm View;

        public LauncherViewModel(MainForm view) : base(true)
        {
            SimpleConfirmHandler = message => MessageBox.Show(View, message, "操作确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk) == DialogResult.OK;
            SimpleWarningHandler = message => MessageBox.Show(View, message, "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            SimpleHandler = (success, message) => MessageBox.Show(View, message, success ? "操作成功" : "操作失败", MessageBoxButtons.OK, success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            SimpleFailureHandler = (success, message) =>
            {
                if (!success)
                {
                    MessageBox.Show(View, message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            View = view;
            Dispatcher = new DispatcherWrapper(a => View.Invoke(a), a => View.BeginInvoke(a), () => !View.InvokeRequired);

            var settings = Properties.Settings.Default;
            if (settings.UpgradeRequired)
            {
                settings.Upgrade();
                settings.UpgradeRequired = false;
                settings.Save();
            }

            LogTextWrapping = settings.LogTextWrapping;
            NotificationMode = settings.SuppressInfo ? 1 : 0;
        }

        public override void ClearLog() => Dispatcher.Invoke(() => View.textBox_log.Clear());

        public override void Log(Log l, bool init)
        {
            if (l.Category == Category.Alert)
            {
                return;
            }
            string category = "";
            switch (l.Level)
            {
            case Level.Debug:
                category = "D ";
                break;
            default:
            case Level.Info:
                category = "I ";
                break;
            case Level.Warn:
                category = "W ";
                break;
            case Level.Error:
                category = "E ";
                break;
            case Level.Fatal:
                category = "F ";
                break;
            }
            if (l.Level != 0)
            {
                l.Data = Utils.ParseTimestamp(l.Time).ToString("yyyy/MM/dd HH:mm:ss") + " " + l.Data;
            }
            Dispatcher.Invoke(() => View.textBox_log.AppendText(l.Source + " " + category + l.Data + Environment.NewLine));
        }

        public override void Save()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Save());
                return;
            }
            var settings = Properties.Settings.Default;

            settings.SuppressInfo = NotificationMode != 0;
            settings.LogTextWrapping = LogTextWrapping;

            settings.Save();
        }
    }
}

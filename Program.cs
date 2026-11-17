using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Taskbar Control")]
[assembly: AssemblyProduct("Taskbar Control")]
[assembly: AssemblyDescription("Keep the Windows taskbar hidden while selected apps are running.")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace TaskbarControl
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool background = Array.Exists(args, a => String.Equals(a, "--background", StringComparison.OrdinalIgnoreCase));
            bool first;
            using (var instance = new Mutex(true, @"Local\TaskbarControl.84523A21", out first))
            using (var openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TaskbarControl.Settings.84523A21"))
            {
                if (!first)
                {
                    if (!background)
                    {
                        // The newly launched process may bring a window forward.
                        // Pass that permission to the instance that owns Settings.
                        using (var self = Process.GetCurrentProcess())
                        foreach (var peer in Process.GetProcessesByName(self.ProcessName))
                        using (peer)
                        {
                            try
                            {
                                if (peer.Id != self.Id && peer.SessionId == self.SessionId &&
                                    String.Equals(Apps.ImagePath((uint)peer.Id), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                                    AllowSetForegroundWindow(peer.Id);
                            }
                            catch (InvalidOperationException) { } // Peer already exited.
                        }
                        openSettings.Set();
                    }
                    return;
                }
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    using (var context = new TrayContext(openSettings, background))
                        Application.Run(context);
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.Message, "Taskbar Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly EventWaitHandle openSettings;
        private readonly Apps apps = new Apps();
        private readonly Taskbars bars = new Taskbars();
        private readonly Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly Timer scan = new Timer { Interval = 650 };
        private readonly Timer shell = new Timer { Interval = 60 };
        private readonly object shellLock = new object();
        private SettingsForm settings;
        private bool active, exiting;

        internal TrayContext(EventWaitHandle openSettings, bool background)
        {
            this.openSettings = openSettings;
            menu.Items.Add("Settings…", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit Taskbar Control", null, delegate { ExitThread(); });
            tray.Icon = icon;
            tray.Text = "Taskbar Control";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowSettings(); };
            tray.Visible = true;
            scan.Tick += delegate
            {
                if (this.openSettings.WaitOne(0)) ShowSettings();
                RefreshState();
            };
            shell.Tick += delegate { lock (shellLock) { if (!exiting && active) bars.Update(); } };
            SystemEvents.SessionEnding += OnSessionEnding;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            RefreshState();
            scan.Start();
            if (!background) ShowSettings();
        }

        private void RefreshState()
        {
            bool? running = apps.AnyRunning();
            lock (shellLock)
            {
                if (exiting || !running.HasValue) return;
                bool changed = active != running.Value;
                active = running.Value;
                if (changed && active)
                {
                    bars.Update();
                    shell.Start();
                }
                if (!active)
                {
                    shell.Stop();
                    bars.Restore();
                }
                tray.Text = active ? "Taskbar Control · Blocking edge reveal" : "Taskbar Control · Waiting for apps";
                if (settings != null) settings.SetStatus(active);
            }
        }

        private void ShowSettings()
        {
            if (settings == null || settings.IsDisposed)
            {
                settings = new SettingsForm(apps, RefreshState, ExitThread);
                settings.Icon = icon;
                settings.FormClosed += delegate { settings = null; };
            }
            settings.SetStatus(active);
            if (settings.WindowState == FormWindowState.Minimized) settings.WindowState = FormWindowState.Normal;
            settings.Show();
            // A launcher can request SW_HIDE for the process's first top-level
            // window. Explicitly show it after WinForms has created the handle.
            ShowWindow(settings.Handle, 5);
            settings.Activate();
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int command);

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            // If another app cancels sign-out, the next shell tick resumes monitoring.
            lock (shellLock) { bars.Restore(); }
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            lock (shellLock) { exiting = true; bars.Restore(); }
        }

        protected override void ExitThreadCore()
        {
            lock (shellLock) { exiting = true; bars.Restore(); }
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                scan.Dispose();
                shell.Dispose();
                SystemEvents.SessionEnding -= OnSessionEnding;
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                lock (shellLock) { exiting = true; bars.Dispose(); }
                if (settings != null) settings.Dispose();
                tray.Visible = false;
                tray.Dispose();
                menu.Dispose();
                icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

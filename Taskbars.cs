using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TaskbarControl
{
    internal sealed class Taskbars : IDisposable
    {
        private readonly HashSet<IntPtr> owned = new HashSet<IntPtr>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly string journal = "RestoreSession" + Process.GetCurrentProcess().SessionId;
        private uint explorerPid, foregroundPid;
        private string explorerIdentity;
        private bool foregroundIsStart;
        private long lastStart = -1000;
        private IAppVisibility launcher;

        internal Taskbars()
        {
            try { launcher = (IAppVisibility)new AppVisibility(); }
            catch (COMException) { } // The foreground Start host is also checked.
            IdentifyShell();
        }

        private static List<IntPtr> Windows()
        {
            var bars = new List<IntPtr>();
            IntPtr primary = FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero) bars.Add(primary);
            IntPtr secondary = IntPtr.Zero;
            while ((secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
                bars.Add(secondary);
            return bars;
        }

        private static uint Owner(IntPtr hwnd)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return pid;
        }

        private void IdentifyShell()
        {
            uint current = Owner(FindWindow("Shell_TrayWnd", null));
            if (current == 0 || current == explorerPid) return;
            long started;
            try { using (var explorer = Process.GetProcessById((int)current)) started = explorer.StartTime.ToFileTimeUtc(); }
            catch (ArgumentException) { return; }
            catch (InvalidOperationException) { return; }
            catch (System.ComponentModel.Win32Exception) { return; }
            owned.Clear();
            explorerPid = current;
            explorerIdentity = current + "/" + started.ToString(CultureInfo.InvariantCulture);
            // A forced exit can leave Explorer windows hidden. Only reclaim our own
            // handles if they still belong to the very same Explorer instance.
            using (var settings = Registry.CurrentUser.CreateSubKey(Apps.SettingsKey))
            {
                var previous = settings.GetValue(journal) as string[];
                if (previous == null) return;
                if (previous.Length > 0 && previous[0] == explorerIdentity)
                {
                    var bars = Windows();
                    foreach (string value in previous.Skip(1))
                    {
                        long raw;
                        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out raw)) continue;
                        var hwnd = new IntPtr(raw);
                        if (bars.Contains(hwnd) && Owner(hwnd) == current)
                            ShowWindowAsync(hwnd, 8); // SW_SHOWNA, preserve foreground
                    }
                }
                settings.DeleteValue(journal, false);
            }
        }

        private void SaveJournal()
        {
            using (var settings = Registry.CurrentUser.CreateSubKey(Apps.SettingsKey))
            {
                if (owned.Count == 0) settings.DeleteValue(journal, false);
                else settings.SetValue(journal, new[] { explorerIdentity }.Concat(
                    owned.Select(h => h.ToInt64().ToString(CultureInfo.InvariantCulture))).ToArray(), RegistryValueKind.MultiString);
            }
        }

        private bool StartIsOpen()
        {
            bool visible;
            if (launcher != null && launcher.IsLauncherVisible(out visible) >= 0 && visible) return true;
            IntPtr foreground = GetForegroundWindow();
            uint pid = Owner(foreground);
            if (pid != foregroundPid)
            {
                foregroundPid = pid;
                foregroundIsStart = String.Equals(Path.GetFileName(Apps.ImagePath(pid)), "StartMenuExperienceHost.exe", StringComparison.OrdinalIgnoreCase);
            }
            return foregroundIsStart && IsWindowVisible(foreground);
        }

        internal void Update()
        {
            IdentifyShell();
            if (explorerIdentity == null) return;
            long now = clock.ElapsedMilliseconds;
            if (StartIsOpen()) lastStart = now;
            // Explorer can dismiss Start on mouse-down. Let the matching mouse-up
            // reach the taskbar button before hiding its window.
            bool reveal = now - lastStart < 180;
            if (reveal && Control.MouseButtons != MouseButtons.None) lastStart = now;
            var bars = Windows();
            if (owned.RemoveWhere(h => !bars.Contains(h)) != 0) SaveJournal();
            foreach (IntPtr bar in bars)
            {
                if (Owner(bar) != explorerPid) continue;
                bool visible = IsWindowVisible(bar);
                if (!reveal && visible)
                {
                    if (owned.Add(bar)) SaveJournal();
                    ShowWindowAsync(bar, 0); // SW_HIDE
                }
                else if (reveal && !visible && owned.Contains(bar))
                    ShowWindowAsync(bar, 8);
            }
        }

        internal void Restore()
        {
            lastStart = -1000;
            if (owned.Count == 0) return;
            var bars = Windows();
            foreach (IntPtr bar in owned.ToArray())
            {
                if (!bars.Contains(bar) || Owner(bar) != explorerPid || ShowWindowAsync(bar, 8))
                    owned.Remove(bar);
            }
            SaveJournal();
        }

        public void Dispose()
        {
            Restore();
            if (launcher != null)
            {
                Marshal.ReleaseComObject(launcher);
                launcher = null;
            }
        }

        [ComImport, Guid("7E5FE3D9-985F-4908-91F9-EE19F9FD1514")]
        private class AppVisibility { }
        [ComImport, Guid("2246EA2D-CAEA-4444-A3C4-6DE827E44313"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAppVisibility
        {
            [PreserveSig] int GetAppVisibilityOnMonitor(IntPtr monitor, out int mode);
            [PreserveSig] int IsLauncherVisible([MarshalAs(UnmanagedType.Bool)] out bool visible);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string name);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hwnd, int command);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}

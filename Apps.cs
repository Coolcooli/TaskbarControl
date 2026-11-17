using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace TaskbarControl
{
    internal sealed class Apps
    {
        internal const string SettingsKey = @"Software\TaskbarControl";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "TaskbarControl";
        private readonly int session = Process.GetCurrentProcess().SessionId;
        private HashSet<string> paths;
        private HashSet<string> filenames;
        internal bool AutoStart { get; private set; }
        internal IEnumerable<string> Paths { get { return paths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase); } }

        internal Apps()
        {
            using (var settings = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                SetPaths(settings.GetValue("Apps") as string[] ?? new string[0]);
                AutoStart = (int)settings.GetValue("StartAtSignIn", 1) != 0;
            }
            ConfigureAutoStart(AutoStart);
        }

        internal void ConfigureAutoStart(bool enabled)
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
            using (var settings = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                if (enabled)
                {
                    string command = "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --background";
                    if (!String.Equals(command, run.GetValue(RunName) as string, StringComparison.Ordinal))
                        run.SetValue(RunName, command);
                }
                else run.DeleteValue(RunName, false);
                settings.SetValue("StartAtSignIn", enabled ? 1 : 0, RegistryValueKind.DWord);
            }
            AutoStart = enabled;
        }

        internal void SavePaths(IEnumerable<string> selection)
        {
            var next = selection.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            using (var settings = Registry.CurrentUser.CreateSubKey(SettingsKey))
                settings.SetValue("Apps", next, RegistryValueKind.MultiString);
            SetPaths(next);
        }

        private void SetPaths(IEnumerable<string> selection)
        {
            paths = new HashSet<string>(selection, StringComparer.OrdinalIgnoreCase);
            filenames = new HashSet<string>(paths.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
        }

        // A failed snapshot is different from a successful snapshot with no matching apps.
        internal bool? AnyRunning()
        {
            if (paths.Count == 0) return false;
            using (var snapshot = CreateToolhelp32Snapshot(2, 0))
            {
                if (snapshot.IsInvalid) return null;
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                if (!Process32First(snapshot, ref entry)) return null;
                do
                {
                    uint processSession;
                    if (filenames.Contains(entry.ExeFile) &&
                        ProcessIdToSessionId(entry.ProcessId, out processSession) && processSession == session &&
                        paths.Contains(ImagePath(entry.ProcessId))) return true;
                } while (Process32Next(snapshot, ref entry));
                return Marshal.GetLastWin32Error() == 18 ? (bool?)false : null; // ERROR_NO_MORE_FILES
            }
        }

        internal static string ImagePath(uint pid)
        {
            using (var process = OpenProcess(0x1000, false, pid)) // PROCESS_QUERY_LIMITED_INFORMATION
            {
                if (process.IsInvalid) return null;
                var path = new StringBuilder(32768);
                int length = path.Capacity;
                return QueryFullProcessImageName(process, 0, path, ref length) ? path.ToString() : null;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            public uint Size, Usage, ProcessId;
            public UIntPtr HeapId;
            public uint ModuleId, Threads, ParentId;
            public int BasePriority;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32First(SafeFileHandle snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32Next(SafeFileHandle snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")]
        private static extern bool ProcessIdToSessionId(uint pid, out uint session);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeFileHandle OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(SafeFileHandle process, int flags, StringBuilder path, ref int length);
    }
}

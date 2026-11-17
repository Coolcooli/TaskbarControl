using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TaskbarControl
{
    internal sealed class SettingsForm : Form
    {
        private readonly Apps apps;
        private readonly Action changed;
        private readonly ListView list = AppList();
        private readonly Button remove = ButtonFor("Remove");
        private readonly Label status = new Label { AutoSize = true };
        private readonly CheckBox startup = new CheckBox { Text = "Start in the background when I sign in", AutoSize = true };

        internal SettingsForm(Apps apps, Action changed, Action exit)
        {
            this.apps = apps;
            this.changed = changed;
            Text = "Taskbar Control";
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(690, 458);
            MinimumSize = new Size(600, 420);
            BackColor = SystemColors.Window;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 14), ColumnCount = 1, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < layout.RowCount; i++)
                layout.RowStyles.Add(i == 3 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            layout.Controls.Add(new Label { Text = "Watched apps", Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
                AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
            layout.Controls.Add(new Label { Text = "The taskbar stays hidden while any listed app runs. Press the Windows key to show it.",
                Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 12) }, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            var browse = ButtonFor("Add files…");
            var running = ButtonFor("Choose running app…");
            browse.Click += delegate { Browse(); };
            running.Click += delegate { ChooseRunning(); };
            remove.Click += delegate
            {
                var selected = new HashSet<string>(list.SelectedItems.Cast<ListViewItem>().Select(item => (string)item.Tag), StringComparer.OrdinalIgnoreCase);
                Save(apps.Paths.Where(path => !selected.Contains(path)));
            };
            remove.Enabled = false;
            actions.Controls.AddRange(new Control[] { browse, running, remove });
            layout.Controls.Add(actions, 0, 2);
            list.SelectedIndexChanged += delegate { remove.Enabled = list.SelectedItems.Count != 0; };
            layout.Controls.Add(list, 0, 3);
            status.Margin = new Padding(0, 7, 0, 10);
            layout.Controls.Add(status, 0, 4);
            startup.Margin = new Padding(0, 0, 0, 9);
            startup.Checked = apps.AutoStart;
            startup.Click += delegate
            {
                try { apps.ConfigureAutoStart(startup.Checked); }
                catch (Exception e) { startup.Checked = apps.AutoStart; Report(e); }
            };
            layout.Controls.Add(startup, 0, 5);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = Padding.Empty, ColumnCount = 2 };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var quit = new LinkLabel { Text = "Exit Taskbar Control", AutoSize = true, Anchor = AnchorStyles.Left,
                Margin = Padding.Empty, LinkBehavior = LinkBehavior.HoverUnderline };
            quit.LinkClicked += delegate { exit(); };
            var done = ButtonFor("Done");
            done.Margin = Padding.Empty;
            done.Click += delegate { Close(); };
            footer.Controls.Add(quit, 0, 0);
            footer.Controls.Add(done, 1, 0);
            layout.Controls.Add(footer, 0, 6);
            CancelButton = done;
            RefreshList();
        }

        internal void SetStatus(bool active)
        {
            string next = active ? "Active · Edge reveal is blocked." :
                apps.Paths.Any() ? "Ready · Waiting for a watched app." : "Ready · Add an app to start watching.";
            if (status.Text != next) status.Text = next;
        }

        private static Button ButtonFor(string text)
        {
            return new Button { Text = text, AutoSize = true, MinimumSize = new Size(82, 29),
                Margin = new Padding(0, 0, 8, 8), UseVisualStyleBackColor = true };
        }

        private static ListView AppList()
        {
            var view = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false,
                ShowItemToolTips = true, Dock = DockStyle.Fill, Margin = Padding.Empty, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            view.Columns.Add("App", 185);
            view.Columns.Add("Executable path", 360);
            view.SizeChanged += delegate { view.Columns[1].Width = Math.Max(140, view.ClientSize.Width - view.Columns[0].Width - 20); };
            return view;
        }

        private static void AddRow(ListView view, string path)
        {
            var item = new ListViewItem(Path.GetFileName(path)) { Tag = path, ToolTipText = path };
            item.SubItems.Add(path);
            view.Items.Add(item);
        }

        private void RefreshList()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (string path in apps.Paths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)) AddRow(list, path);
            list.EndUpdate();
            remove.Enabled = false;
        }

        private void Save(IEnumerable<string> paths)
        {
            try
            {
                apps.SavePaths(paths);
                RefreshList();
                changed();
            }
            catch (Exception e) { Report(e); }
        }

        private void Report(Exception error)
        {
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Browse()
        {
            using (var dialog = new OpenFileDialog { Title = "Choose apps to watch", Filter = "Applications (*.exe)|*.exe", Multiselect = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) Save(apps.Paths.Concat(dialog.FileNames));
        }

        private void ChooseRunning()
        {
            using (var dialog = new Form { Text = "Choose running apps", Font = Font, Icon = Icon, ClientSize = new Size(590, 330),
                MinimizeBox = false, MaximizeBox = false, StartPosition = FormStartPosition.CenterParent, MinimumSize = new Size(400, 250) })
            {
                var view = AppList();
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var self = Process.GetCurrentProcess())
                foreach (var process in Process.GetProcesses())
                using (process)
                {
                    try
                    {
                        if (process.Id == self.Id || process.MainWindowHandle == IntPtr.Zero || process.SessionId != self.SessionId) continue;
                        string path = Apps.ImagePath((uint)process.Id);
                        if (path != null) paths.Add(path);
                    }
                    catch (InvalidOperationException) { } // App exited during enumeration.
                    catch (Win32Exception) { } // Inaccessible system process.
                }
                foreach (string path in paths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)) AddRow(view, path);
                var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
                var cancel = ButtonFor("Cancel");
                cancel.DialogResult = DialogResult.Cancel;
                var add = ButtonFor("Add selected");
                add.DialogResult = DialogResult.OK;
                add.Enabled = false;
                view.SelectedIndexChanged += delegate { add.Enabled = view.SelectedItems.Count > 0; };
                view.DoubleClick += delegate { if (add.Enabled) dialog.DialogResult = DialogResult.OK; };
                footer.Controls.AddRange(new Control[] { cancel, add });
                dialog.Padding = new Padding(14);
                dialog.Controls.Add(view);
                dialog.Controls.Add(footer);
                dialog.AcceptButton = add;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Save(apps.Paths.Concat(view.SelectedItems.Cast<ListViewItem>().Select(item => (string)item.Tag)));
            }
        }
    }
}

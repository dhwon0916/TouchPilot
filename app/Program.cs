using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace TouchPilot;

internal static class Program
{
    static readonly object LogLock = new();
    internal static void Log(string message)
    {
        lock (LogLock) try
        {
            Directory.CreateDirectory(Preferences.DataFolder);
            var path = Path.Combine(Preferences.DataFolder, "startup.log");
            if (File.Exists(path) && new FileInfo(path).Length > 65536) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        } catch { }
    }
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\TouchPilot.App", out var owner);
        using var show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TouchPilot.Show");
        if (!owner) { if (!args.Contains("--background")) show.Set(); return; }
        try
        {
            using var form = new SettingsWindow(args.Contains("--background"));
            var registration = ThreadPool.RegisterWaitForSingleObject(show, (_, _) => { try { if (!form.IsDisposed) form.BeginInvoke(form.Reveal); } catch (InvalidOperationException) { } }, null, -1, false);
            try { Application.Run(form); } finally { registration.Unregister(null); }
        }
        catch (Exception e) { Log(e.ToString()); MessageBox.Show(e.Message, "TouchPilot could not start", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { mutex.ReleaseMutex(); }
    }
}

internal sealed class SettingsWindow : Form
{
    readonly InputService service = new();
    Preferences saved;
    readonly NotifyIcon tray;
    readonly Icon appIcon;
    readonly System.Windows.Forms.Timer health = new() { Interval = 3000 };
    readonly CheckBox enabled = Check("Enable touch independence");
    readonly CheckBox stylus = Check("Include stylus");
    readonly CheckBox focus = Check("Restore typing focus after touch");
    readonly CheckBox movement = Check("Restore focus when the mouse moves");
    readonly CheckBox all = Check("Use all displays");
    readonly CheckBox startup = Check("Start with Windows");
    readonly NumericUpDown delay = new() { Minimum = 0, Maximum = 5000, Increment = 20, Width = 110 };
    readonly ComboBox modifier = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    readonly CheckedListBox screens = new() { CheckOnClick = true, Height = 84, Width = 515 };
    readonly TextBox keep = new() { Width = 515, PlaceholderText = "Example: browser.exe; editor.exe", MaxLength = 2048 };
    readonly TextBox restore = new() { Width = 515, PlaceholderText = "Leave empty to allow all other apps", MaxLength = 2048 };
    readonly Label status = new() { AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
    readonly FlowLayoutPanel advanced;
    readonly Button save = new() { Text = "Save & apply", AutoSize = true, Padding = new Padding(14, 5, 14, 5) };
    readonly ToolStripMenuItem pause = new("Pause");
    bool exiting, paused, busy, failed;
    readonly bool background;
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    static CheckBox Check(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
    static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 8, 0, 4) };
    static FlowLayoutPanel Column() => new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
    public SettingsWindow(bool startHidden)
    {
        background = startHidden;
        Text = "TouchPilot"; StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(245, 247, 251);
        ClientSize = new Size(590, 810); MinimumSize = new Size(580, 480);
        using (var bitmap = new Bitmap(32, 32))
        {
            using var g = Graphics.FromImage(bitmap); g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(35, 91, 186)); g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 19, FontStyle.Bold, GraphicsUnit.Pixel); g.DrawString("T", font, Brushes.White, 7, 3);
            var handle = bitmap.GetHicon(); appIcon = (Icon)Icon.FromHandle(handle).Clone(); DestroyIcon(handle);
        }
        Icon = appIcon;
        try { saved = Preferences.Load(); } catch (Exception e) { Program.Log(e.ToString()); saved = new(); MessageBox.Show("Could not read your saved settings. Defaults are shown; the saved file is kept until you apply.", "TouchPilot"); }
        var root = Column(); root.Padding = new Padding(24, 18, 24, 18); root.Dock = DockStyle.Fill; root.AutoScroll = true; root.AutoSize = false;
        root.Controls.Add(new Label { Text = "TouchPilot", Font = new Font(Font, FontStyle.Bold), AutoSize = true });
        root.Controls.Add(Caption("Keep your mouse position and typing focus while using touch."));
        root.Controls.Add(enabled);
        advanced = Column(); advanced.Controls.Add(stylus); advanced.Controls.Add(focus);
        var delayRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        delayRow.Controls.Add(Caption("Focus delay (ms)  ")); delayRow.Controls.Add(delay); advanced.Controls.Add(delayRow);
        advanced.Controls.Add(movement); advanced.Controls.Add(Caption("Displays")); advanced.Controls.Add(all); advanced.Controls.Add(screens);
        advanced.Controls.Add(Caption("Hold this key while tapping to keep control in the touched app"));
        modifier.Items.AddRange(["None", "Ctrl", "Alt", "Shift", "Win"]); advanced.Controls.Add(modifier);
        advanced.Controls.Add(Caption("Apps that keep typing focus")); advanced.Controls.Add(keep);
        advanced.Controls.Add(Caption("Only restore typing after touching these apps (optional)")); advanced.Controls.Add(restore);
        advanced.Controls.Add(Caption("Use executable names separated by semicolons. Keep-focus rules win."));
        root.Controls.Add(advanced);
        var footer = Column(); footer.Dock = DockStyle.Bottom; footer.Padding = new Padding(24, 8, 24, 14);
        footer.Controls.Add(startup); footer.Controls.Add(save); footer.Controls.Add(status);
        var hint = Caption("Close to keep running in the tray. Choose Exit there to stop.");
        hint.MaximumSize = new Size(515, 0); footer.Controls.Add(hint);
        Controls.Add(root); Controls.Add(footer);
        var menu = new ContextMenuStrip(); menu.Items.Add("Settings", null, (_, _) => Reveal()); menu.Items.Add(pause);
        menu.Items.Add("Open settings folder", null, (_, _) => { Directory.CreateDirectory(Preferences.DataFolder); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Preferences.DataFolder) { UseShellExecute = true }); });
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit", null, (_, _) => { exiting = true; Close(); });
        tray = new NotifyIcon { Text = "TouchPilot", Icon = appIcon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Reveal(); pause.Click += async (_, _) => { paused = !paused; pause.Text = paused ? "Resume" : "Pause"; await ApplySaved(); };
        enabled.Checked = saved.Enabled; stylus.Checked = saved.Stylus; focus.Checked = saved.RestoreFocus;
        delay.Value = saved.Delay; movement.Checked = saved.OnMouseMove; all.Checked = saved.AllDisplays;
        modifier.SelectedItem = modifier.Items.Contains(saved.Modifier) ? saved.Modifier : "None";
        keep.Text = saved.KeepApps; restore.Text = saved.RestoreApps; startup.Checked = saved.StartWithWindows;
        RefreshScreens(); UpdateControls();
        enabled.CheckedChanged += (_, _) => UpdateControls(); focus.CheckedChanged += (_, _) => UpdateControls();
        movement.CheckedChanged += (_, _) => UpdateControls(); all.CheckedChanged += (_, _) => UpdateControls();
        save.Click += async (_, _) => await Save();
        health.Tick += async (_, _) => { if (!busy && !failed && !service.Running) await ApplySaved(); };
        Shown += async (_, _) => { if (background) Hide(); await ApplySaved(); health.Start(); };
        FormClosing += (_, e) => { if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
    }
    public void Reveal() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void UpdateControls() { advanced.Visible = enabled.Checked; delay.Enabled = focus.Checked && !movement.Checked; movement.Enabled = focus.Checked; keep.Enabled = restore.Enabled = focus.Checked; screens.Enabled = !all.Checked; }
    void RefreshScreens()
    {
        screens.Items.Clear(); foreach (var s in Screen.AllScreens) screens.Items.Add(new DisplayChoice(s), saved.Displays.Contains(s.DeviceName, StringComparer.OrdinalIgnoreCase));
    }
    sealed record DisplayChoice(Screen Screen) { public override string ToString() => $"{Screen.DeviceName}   {Screen.Bounds.Width} x {Screen.Bounds.Height}" + (Screen.Primary ? "   (primary)" : ""); }
    async Task Save()
    {
        if (busy) return;
        var selected = screens.CheckedItems.Cast<DisplayChoice>().Select(c => c.Screen.DeviceName).ToArray();
        var next = new Preferences { Enabled = enabled.Checked, Stylus = stylus.Checked, RestoreFocus = focus.Checked, Delay = (int)delay.Value, OnMouseMove = movement.Checked, AllDisplays = all.Checked, Displays = selected, Modifier = modifier.Text, KeepApps = keep.Text.Trim(), RestoreApps = restore.Text.Trim(), StartWithWindows = startup.Checked };
        try { Startup.Set(next.StartWithWindows); next.Save(); saved = next; failed = false; await ApplySaved(); }
        catch (Exception e) { Error(e); }
    }
    async Task ApplySaved()
    {
        if (busy || exiting) return; busy = true; save.Enabled = false;
        try { await service.Apply(saved, paused); failed = false; status.Text = paused ? "Paused" : saved.Enabled ? "Touch independence is active" : "Touch independence is off"; tray.Text = "TouchPilot — " + (paused ? "paused" : saved.Enabled ? "active" : "off"); }
        catch (Exception e) { failed = true; if (!exiting) Error(e); }
        finally { busy = false; if (!IsDisposed) save.Enabled = true; }
    }
    void Error(Exception e) { Program.Log(e.ToString()); status.Text = "Could not apply settings"; Reveal(); MessageBox.Show(this, e.Message, "TouchPilot", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x007e && IsHandleCreated) BeginInvoke(async () => { RefreshScreens(); await ApplySaved(); });
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { exiting = true; health.Stop(); health.Dispose(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); service.Dispose(); appIcon.Dispose(); }
        base.Dispose(disposing);
    }
}

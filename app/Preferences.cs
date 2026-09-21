using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
namespace TouchPilot;

public sealed record Preferences
{
    public bool Enabled { get; set; }
    public bool Stylus { get; set; }
    public bool RestoreFocus { get; set; }
    public int Delay { get; set; } = 120;
    public bool OnMouseMove { get; set; }
    public bool AllDisplays { get; set; } = true;
    public string[] Displays { get; set; } = [];
    public string Modifier { get; set; } = "None";
    public string KeepApps { get; set; } = "";
    public string RestoreApps { get; set; } = "";
    public bool StartWithWindows { get; set; }
    public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TouchPilot");
    public static string FilePath => Path.Combine(DataFolder, "settings.json");
    public static Preferences Load(string? path = null)
    {
        path ??= FilePath;
        if (!File.Exists(path)) return new();
        var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new();
        p.Delay = Math.Clamp(p.Delay, 0, 5000); p.Displays ??= []; p.Modifier ??= "None";
        p.KeepApps ??= ""; p.RestoreApps ??= ""; return p;
    }
    public void Save(string? path = null)
    {
        path ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
    public string EngineConfig(bool paused) => JsonSerializer.Serialize(new
    {
        enabled = Enabled && !paused, restore_focus = RestoreFocus, stylus_mouse_independent = Stylus,
        focus_restore_delay = Delay, focus_restore_on_mouse_move = OnMouseMove,
        touch_all_displays = AllDisplays, touch_display_bounds = string.Join(';', Screen.AllScreens
            .Where(s => Displays.Contains(s.DeviceName, StringComparer.OrdinalIgnoreCase))
            .Select(s => $"{s.Bounds.X},{s.Bounds.Y},{s.Bounds.Width},{s.Bounds.Height}")),
        touch_override_modifier = Modifier, focus_keep_apps = KeepApps, focus_restore_apps = RestoreApps
    });
}

internal sealed class InputService : IDisposable
{
    Process? process;
    readonly SemaphoreSlim gate = new(1, 1);
    public bool Running => process is { HasExited: false };
    public async Task Apply(Preferences preferences, bool paused)
    {
        await gate.WaitAsync();
        try
        {
            if (!Running)
            {
                process?.Dispose();
                var executable = Path.Combine(AppContext.BaseDirectory, "TouchPilot.Input.exe");
                if (!File.Exists(executable)) throw new FileNotFoundException("Keep TouchPilot.Input.exe beside TouchPilot.exe. Extract the entire ZIP.");
                var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = AppContext.BaseDirectory };
                process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the input service.");
                process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Program.Log(e.Data); };
                process.BeginErrorReadLine();
                var ready = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (ready != "READY") throw new InvalidOperationException("The input service did not start. See startup.log in the settings folder.");
            }
            await process!.StandardInput.WriteLineAsync(preferences.EngineConfig(paused));
            await process.StandardInput.FlushAsync();
        }
        catch
        {
            Stop(); throw;
        }
        finally { gate.Release(); }
    }
    void Stop()
    {
        if (process is null) return;
        try { if (!process.HasExited) { process.StandardInput.Close(); if (!process.WaitForExit(1500)) process.Kill(); } }
        catch (InvalidOperationException) { }
        finally { process.Dispose(); process = null; }
    }
    public void Dispose() { Stop(); }
}

internal static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("TouchPilot", $"\"{Environment.ProcessPath}\" --background");
        else key.DeleteValue("TouchPilot", false);
    }
}

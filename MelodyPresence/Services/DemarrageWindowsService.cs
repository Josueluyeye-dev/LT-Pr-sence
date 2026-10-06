using System.Diagnostics;
using Microsoft.Win32;

namespace MelodyPresence.Services;

public static class DemarrageWindowsService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LTServicesPresence";

    public static string ExePath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? AppContext.BaseDirectory;

    public static void Appliquer(bool demarrerAvecWindows)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (demarrerAvecWindows)
        {
            var cmd = $"\"{ExePath}\" --autostart";
            key.SetValue(ValueName, cmd);
        }
        else if (key.GetValue(ValueName) != null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool EstEnregistre()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) != null;
    }
}

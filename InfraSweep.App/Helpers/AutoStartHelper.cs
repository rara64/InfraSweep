using System;
using System.IO;
using System.Runtime.InteropServices;

#if WINDOWS
using Microsoft.Win32;
#endif

namespace InfraSweep.App.Helpers;

public class AutoStartHelper
{
    private static string AppName => AppDomain.CurrentDomain.FriendlyName;
    private static string AppPath => Environment.ProcessPath ?? string.Empty;
    private static string FreeDesktopEntry => $"""
        [Desktop Entry]
        Type=Application
        Name={AppName}
        Exec="{AppPath}" -silent
        Terminal=false
        X-GNOME-Autostart-enabled=true
        """;
    private static string MacOsEntry => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0/EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key>
            <string>com.{AppName.ToLower()}</string>
            <key>ProgramArguments</key>
            <array>
                <string>{AppPath}</string>
                <string>-silent</string>
            </array>
            <key>RunAtLoad</key>
            <true/>
        </dict>
        </plist>
        """;

    public static void Set(bool value)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SetWindowsAutoStart(value);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            SetLinuxAutoStart(value);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            SetOSXAutostart(value);
    }

    private static void SetWindowsAutoStart(bool value)
    {
        #if WINDOWS
        using var key = Registry.CurrentUser
            .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        
        if (value)
            key?.SetValue(AppName, $"\"{AppPath}\" -silent");
        else
            key?.DeleteValue(AppName, false);
        #endif
    }

    private static void SetLinuxAutoStart(bool value)
    {
        string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".config");

        string autostartDir = Path.Combine(configHome, "autostart");
        string filePath = Path.Combine(autostartDir, $"{AppName.ToLower()}.desktop");

        ToggleEntryInAutostartDirectory(FreeDesktopEntry, autostartDir, filePath, value);
    }

    private static void SetOSXAutostart(bool value)
    {
        string autostartDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents");
        string filePath = Path.Combine(autostartDir, $"com.{AppName.ToLower()}.plist");

        ToggleEntryInAutostartDirectory(MacOsEntry, autostartDir, filePath, value);
    }

    private static void ToggleEntryInAutostartDirectory(string content, string autostartDir, string filePath, bool value)
    {
        if (value)
        {
            Directory.CreateDirectory(autostartDir);
            File.WriteAllText(filePath, content);
        }
        else if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
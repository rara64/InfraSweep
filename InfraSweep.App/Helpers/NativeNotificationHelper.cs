using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

#if WINDOWS
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using System.Security;
using Microsoft.Win32;
#endif

namespace InfraSweep.App.Helpers;

public class NativeNotificationHelper
{
    public static async Task Send(string title, string message)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            #if WINDOWS
            _= SendWindowsNotification(title, message);
            #endif   
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _= SendLinuxNotification(title, message);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            _= SendOSXNotification(title, message);
    }

    private static async Task SendWindowsNotification(string title, string message)
    {
        #if WINDOWS
        string appId = "InfraSweep.App";

        RegisterAppIdInRegistry(appId, "InfraSweep");

        string xmlString = $@"
        <toast>
            <visual>
                <binding template='ToastGeneric'>
                    <text>{SecurityElement.Escape(title)}</text>
                    <text>{SecurityElement.Escape(message)}</text>
                </binding>
            </visual>
        </toast>
        ";

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlString);

        var toast = new ToastNotification(xmlDoc);
        var notifier = ToastNotificationManager.CreateToastNotifier(appId);

        notifier.Show(toast);
        #endif
    }

    private static void RegisterAppIdInRegistry(string appId, string displayName)
    {
        #if WINDOWS
        string subKeyPath = $@"Software\Classes\AppUserModelId\{appId}";

        using var key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        
        if (key != null)
        {
            if (key.GetValue("DisplayName") == null)
            {
                key.SetValue("DisplayName", displayName);
            }
        }
        #endif
    }

    private static async Task SendLinuxNotification(string title, string message)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            processInfo.ArgumentList.Add("-a");
            processInfo.ArgumentList.Add("InfraSweep");

            processInfo.ArgumentList.Add(title);
            processInfo.ArgumentList.Add(message);

            using var process = Process.Start(processInfo);
            process?.WaitForExit(2000);   
        } catch {}
    }

    private static async Task SendOSXNotification(string title, string message)
    {
        try
        {
            var script = $"display alert \"{title}\" message \"{message}\"";

            var processInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            processInfo.ArgumentList.Add("-e");
            processInfo.ArgumentList.Add(script);

           using var process = Process.Start(processInfo);
           process?.WaitForExit(2000);
        } catch{}
    }
}
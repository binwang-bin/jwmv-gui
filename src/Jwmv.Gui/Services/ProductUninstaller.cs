using Microsoft.Win32;

namespace Jwmv.Gui.Services;

/// <summary>
/// Locates the Inno Setup uninstaller for jwmv.
/// Uninstall must only remove the app PATH entry — never JAVA_HOME / *_HOME.
/// </summary>
internal static class ProductUninstaller
{
    private const string UninstallKeyName = "{A7C3E9F1-2B84-4D5A-9E61-8C0F2D4A1B73}_is1";

    public static bool TryResolve(out string fileName, out string arguments, out string? displayName)
    {
        fileName = string.Empty;
        arguments = string.Empty;
        displayName = null;

        var local = Path.Combine(AppContext.BaseDirectory, "unins000.exe");
        if (File.Exists(local))
        {
            fileName = local;
            arguments = string.Empty;
            displayName = "jwmv";
            return true;
        }

        if (TryReadUninstallRegistration(RegistryHive.CurrentUser, out fileName, out arguments, out displayName))
        {
            return true;
        }

        return TryReadUninstallRegistration(RegistryHive.LocalMachine, out fileName, out arguments, out displayName);
    }

    private static bool TryReadUninstallRegistration(
        RegistryHive hive,
        out string fileName,
        out string arguments,
        out string? displayName)
    {
        fileName = string.Empty;
        arguments = string.Empty;
        displayName = null;

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallKeyName}")
            ?? baseKey.OpenSubKey($@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallKeyName}");
        if (key is null)
        {
            return false;
        }

        var uninstallString = key.GetValue("UninstallString") as string;
        if (string.IsNullOrWhiteSpace(uninstallString))
        {
            return false;
        }

        displayName = key.GetValue("DisplayName") as string;
        return TrySplitCommand(uninstallString.Trim(), out fileName, out arguments) && File.Exists(fileName);
    }

    private static bool TrySplitCommand(string command, out string fileName, out string arguments)
    {
        fileName = string.Empty;
        arguments = string.Empty;
        if (command.StartsWith('\"'))
        {
            var end = command.IndexOf('\"', 1);
            if (end <= 1)
            {
                return false;
            }

            fileName = command[1..end];
            arguments = command[(end + 1)..].Trim();
            return true;
        }

        var space = command.IndexOf(' ');
        if (space < 0)
        {
            fileName = command;
            return true;
        }

        fileName = command[..space];
        arguments = command[(space + 1)..].Trim();
        return true;
    }
}

using Jwmv.Core.Abstractions;
using Jwmv.Core.Exceptions;

namespace Jwmv.Infrastructure.Shell;

/// <summary>
/// Resolves the jwmv host executable used by PowerShell integration.
/// The installed app is a single dual-mode binary (GUI with no args, CLI with args).
/// </summary>
internal static class JwmvCliLocator
{
    public static string ResolveCliExecutablePath(IAppContext appContext)
    {
        var configured = FirstExisting(
            appContext.GetEnvironmentVariable("JWMV_CLI"),
            appContext.GetEnvironmentVariable("JWVM_CLI"));
        if (configured is not null)
        {
            return configured;
        }

        var current = appContext.ExecutablePath;
        if (IsJwmvHost(current) && File.Exists(current))
        {
            return Path.GetFullPath(current);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(current));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            var sameDirectory = Path.Combine(directory, GetHostFileName());
            if (File.Exists(sameDirectory))
            {
                return sameDirectory;
            }

            var fromDevTree = TryFindDevCliExecutable(directory);
            if (fromDevTree is not null)
            {
                return fromDevTree;
            }

            var fromBase = TryFindNearApplicationBase(appContext.ApplicationBaseDirectory);
            if (fromBase is not null)
            {
                return fromBase;
            }
        }

        throw new JwmvException(
            "Shell integration could not locate jwmv.exe. Reinstall jwmv, or set the JWMV_CLI environment variable to the full path of jwmv.exe, then run Integrate again.");
    }

    private static string? TryFindNearApplicationBase(string? applicationBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(applicationBaseDirectory) || !Directory.Exists(applicationBaseDirectory))
        {
            return null;
        }

        var sameDirectory = Path.Combine(applicationBaseDirectory, GetHostFileName());
        return File.Exists(sameDirectory) ? Path.GetFullPath(sameDirectory) : TryFindDevCliExecutable(applicationBaseDirectory);
    }

    private static string? TryFindDevCliExecutable(string startDirectory)
    {
        // Dev layout: prefer the dual-mode GUI host, then the console CLI project output.
        var current = new DirectoryInfo(startDirectory);
        for (var depth = 0; depth < 8 && current is not null; depth++, current = current.Parent)
        {
            var guiHost = FindPublishedHostUnder(Path.Combine(current.FullName, "Jwmv.Gui"));
            if (guiHost is not null)
            {
                return guiHost;
            }

            var cliHost = FindPublishedHostUnder(Path.Combine(current.FullName, "Jwmv.Cli"));
            if (cliHost is not null)
            {
                return cliHost;
            }

            if (string.Equals(current.Name, "src", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return null;
    }

    private static string? FindPublishedHostUnder(string projectDirectory)
    {
        if (!Directory.Exists(projectDirectory))
        {
            return null;
        }

        var fileNames = new[] { GetHostFileName(), OperatingSystem.IsWindows() ? "jwmv-gui.exe" : "jwmv-gui" };
        foreach (var fileName in fileNames)
        {
            var candidates = new[]
            {
                Path.Combine(projectDirectory, "bin", "Release", "net8.0-windows", "win-x64", fileName),
                Path.Combine(projectDirectory, "bin", "Release", "net8.0-windows", fileName),
                Path.Combine(projectDirectory, "bin", "Release", "net8.0", "win-x64", fileName),
                Path.Combine(projectDirectory, "bin", "Release", "net8.0", fileName),
                Path.Combine(projectDirectory, "bin", "Debug", "net8.0-windows", fileName),
                Path.Combine(projectDirectory, "bin", "Debug", "net8.0", fileName)
            };

            var match = candidates.FirstOrDefault(File.Exists);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool IsJwmvHost(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return string.Equals(name, "jwmv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "jwmv-gui", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetHostFileName() => OperatingSystem.IsWindows() ? "jwmv.exe" : "jwmv";

    private static string? FirstExisting(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(candidate.Trim());
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}

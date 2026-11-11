using System.Runtime.InteropServices;
using Jwmv.Core.Abstractions;

namespace Jwmv.Infrastructure.Runtime;

public sealed class DefaultAppContext : IAppContext
{
    public string WorkingDirectory => Environment.CurrentDirectory;

    public string UserProfileDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string ApplicationBaseDirectory =>
        Path.GetFullPath(System.AppContext.BaseDirectory);

    public Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;

    public string ExecutablePath => ResolveExecutablePath();

    public string? GetEnvironmentVariable(string variableName) =>
        Environment.GetEnvironmentVariable(variableName);

    private static string ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        var commandPath = Environment.GetCommandLineArgs().FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(commandPath))
        {
            return Path.GetFullPath(commandPath, System.AppContext.BaseDirectory);
        }

        throw new InvalidOperationException("Unable to resolve the current executable path.");
    }
}

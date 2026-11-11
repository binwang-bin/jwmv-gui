using System.Runtime.InteropServices;

namespace Jwmv.Core.Abstractions;

public interface IAppContext
{
    string WorkingDirectory { get; }
    string UserProfileDirectory { get; }

    /// <summary>
    /// Directory that contains the running jwmv / jwmv-gui binaries (portable install root).
    /// </summary>
    string ApplicationBaseDirectory { get; }

    Architecture ProcessArchitecture { get; }
    string ExecutablePath { get; }
    string? GetEnvironmentVariable(string variableName);
}

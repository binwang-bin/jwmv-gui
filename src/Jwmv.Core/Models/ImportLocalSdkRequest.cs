namespace Jwmv.Core.Models;

public sealed record ImportLocalSdkRequest
{
    public string CandidateName { get; init; } = "java";
    public required string Path { get; init; }
    public string? Alias { get; init; }
    public bool SetAsDefault { get; init; }

    /// <summary>
    /// Allows importing a Java runtime that has <c>java</c> but no <c>javac</c> (JRE-only).
    /// Off by default so accidental JRE imports are rejected with a clear message.
    /// </summary>
    public bool AllowJre { get; init; }

    /// <summary>
    /// When the path is a parent directory that contains multiple SDK homes, import every match.
    /// </summary>
    public bool ImportAll { get; init; }
}

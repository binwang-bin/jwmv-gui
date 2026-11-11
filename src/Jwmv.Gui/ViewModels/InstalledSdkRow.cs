using Jwmv.Core.Models;

namespace Jwmv.Gui.ViewModels;

public sealed class InstalledSdkRow
{
    public required InstalledSdkVersion Version { get; init; }

    public bool IsDefault { get; init; }

    public string DefaultLabel { get; init; } = string.Empty;

    public string CandidateName => Version.CandidateName;

    public string Alias => Version.Alias;

    public string VersionText => Version.Version;

    public string DisplayName => Version.DisplayName;

    public string HomeDirectory => Version.HomeDirectory;

    public DateTimeOffset InstalledAtUtc => Version.InstalledAtUtc;
}

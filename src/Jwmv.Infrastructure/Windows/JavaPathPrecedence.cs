using Jwmv.Core.Utilities;

namespace Jwmv.Infrastructure.Windows;

public static class JavaPathPrecedence
{
    public static IReadOnlyList<string> GetMachinePathEntriesThatShadowJava(string? preferredBinDirectory)
    {
        var machinePath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
        var entries = PathTools.SplitPath(machinePath);
        if (entries.Count == 0)
        {
            return [];
        }

        var shadows = new List<string>();
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(preferredBinDirectory)
                && string.Equals(entry, preferredBinDirectory, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (DirectoryContainsJava(entry))
            {
                shadows.Add(entry);
            }
        }

        return shadows;
    }

    public static string BuildShadowWarning(IReadOnlyList<string> shadowingEntries, string preferredBinDirectory)
    {
        var listed = string.Join(", ", shadowingEntries);
        return $"The system (Machine) PATH still contains Java entries before jwmv can win: {listed}. "
            + $"Windows searches Machine PATH before User PATH, so `java -version` may keep showing the old runtime even though JAVA_HOME is {preferredBinDirectory}. "
            + "Remove those Machine PATH entries (often Oracle java8path/javapath), then open a new Windows Terminal tab.";
    }

    private static bool DirectoryContainsJava(string directory)
    {
        try
        {
            return File.Exists(Path.Combine(directory, "java.exe"));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

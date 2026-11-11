namespace Jwmv.Core.Models;

public enum JavaActivationSource
{
    None = 0,
    Default = 1,
    Session = 2,
    Project = 3,

    /// <summary>
    /// Resolved from an already configured process/user HOME variable (JAVA_HOME, MAVEN_HOME, ...).
    /// </summary>
    Environment = 4
}

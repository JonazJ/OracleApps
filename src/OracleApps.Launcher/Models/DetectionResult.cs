namespace OracleApps.Launcher.Models;

/// <summary>Outcome of looking for one app on this computer.</summary>
public sealed record DetectionResult(bool Found, string? ResolvedPath, string? Source)
{
    public static DetectionResult NotFound { get; } = new(false, null, null);

    /// <summary>Apps without detection rules (typically web apps) are always available.</summary>
    public static DetectionResult AlwaysAvailable { get; } = new(true, null, "no detection rules");
}

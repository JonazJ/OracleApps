namespace OracleApps.Launcher.Models;

/// <summary>Root of <c>config/apps.json</c>.</summary>
public sealed class LauncherConfig
{
    /// <summary>Title shown in the header of the launcher window.</summary>
    public string Title { get; set; } = "Oracle Apps";

    /// <summary>Subtitle shown under the title.</summary>
    public string Subtitle { get; set; } = "All your apps in one place";

    /// <summary>
    /// Optional path to a background picture (jpg/png). Environment variables are expanded.
    /// When empty or missing, the built-in background is used.
    /// </summary>
    public string? BackgroundImage { get; set; }

    /// <summary>Apps shown as tiles, in the order they should appear.</summary>
    public List<AppDefinition> Apps { get; set; } = new();
}

/// <summary>A single app tile.</summary>
public sealed class AppDefinition
{
    /// <summary>Stable identifier, used for logging and settings.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Accent colour of the tile as a hex string, e.g. <c>#C74634</c>.</summary>
    public string? Accent { get; set; }

    /// <summary>Optional path to an .ico/.png used for the tile. Environment variables are expanded.</summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// How the app is detected on this computer. When no rules are given the app is always
    /// available (use that for web apps that only need a browser).
    /// </summary>
    public DetectionRules? Detect { get; set; }

    /// <summary>How the app is started.</summary>
    public LaunchSpec? Launch { get; set; }

    /// <summary>Optional page to open when the app is not installed.</summary>
    public string? InstallUrl { get; set; }

    /// <summary>Set to false to hide the tile without deleting it from the config.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>Ways of deciding whether an app is present on this computer.</summary>
public sealed class DetectionRules
{
    /// <summary>Registry keys that must exist, e.g. <c>HKLM\SOFTWARE\Oracle\VirtualBox</c>.</summary>
    public List<string> RegistryKeys { get; set; } = new();

    /// <summary>Registry values to read. The value content is used as the resolved install path.</summary>
    public List<RegistryValueRule> RegistryValues { get; set; } = new();

    /// <summary>
    /// Files or folders to look for. Environment variables are expanded and <c>*</c> wildcards are
    /// supported per path segment, e.g. <c>%ProgramFiles%\Oracle\SQL Developer *\sqldeveloper.exe</c>.
    /// </summary>
    public List<string> Paths { get; set; } = new();

    /// <summary>Executable names resolved through PATH and the Windows "App Paths" registry.</summary>
    public List<string> Executables { get; set; } = new();

    /// <summary>Registered URI schemes, e.g. <c>ms-excel</c>. Checked in HKEY_CLASSES_ROOT.</summary>
    public List<string> UriSchemes { get; set; } = new();
}

/// <summary>A single registry value lookup.</summary>
public sealed class RegistryValueRule
{
    public string Key { get; set; } = string.Empty;

    public string? Name { get; set; }

    /// <summary>Optional path appended to the value when it points at an install folder.</summary>
    public string? Append { get; set; }
}

/// <summary>How a tile starts its app.</summary>
public sealed class LaunchSpec
{
    /// <summary><c>executable</c>, <c>uri</c> or <c>detected</c> (start whatever detection found).</summary>
    public string Kind { get; set; } = "detected";

    /// <summary>Executable path or URI. Environment variables and <c>*</c> wildcards are supported.</summary>
    public string? Target { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// <see cref="Kind"/> as an enum. Spelling is forgiving because apps.json is hand-edited;
    /// anything unrecognised falls back to <see cref="LaunchKind.Detected"/>.
    /// </summary>
    public LaunchKind ResolvedKind => Kind?.Trim().ToLowerInvariant() switch
    {
        "uri" or "url" or "web" or "browser" => LaunchKind.Uri,
        "executable" or "exe" or "app" => LaunchKind.Executable,
        _ => LaunchKind.Detected
    };
}

public enum LaunchKind
{
    /// <summary>Start the file or folder that detection resolved.</summary>
    Detected,

    /// <summary>Start <see cref="LaunchSpec.Target"/> as an executable.</summary>
    Executable,

    /// <summary>Hand <see cref="LaunchSpec.Target"/> to the shell (http(s), mailto, custom schemes).</summary>
    Uri
}

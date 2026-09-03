using System.Windows.Media;

namespace OracleApps.Launcher.Models;

/// <summary>The person the launcher is showing apps for.</summary>
public sealed class UserProfile
{
    public required string DisplayName { get; init; }

    public string? Email { get; init; }

    /// <summary>Profile picture from Microsoft Graph, when available.</summary>
    public ImageSource? Photo { get; init; }

    /// <summary>True when the user chose to continue without signing in.</summary>
    public bool IsLocalMode { get; init; }

    public string Initials
    {
        get
        {
            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant()
            };
        }
    }
}

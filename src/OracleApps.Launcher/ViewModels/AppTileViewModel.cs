using System.Windows.Input;
using System.Windows.Media;
using OracleApps.Launcher.Common;
using OracleApps.Launcher.Models;

namespace OracleApps.Launcher.ViewModels;

/// <summary>One app tile in the grid.</summary>
public sealed class AppTileViewModel : ObservableObject
{
    private static readonly Brush ReadyBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)));
    private static readonly Brush MissingBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)));
    private static readonly Brush DefaultAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xC7, 0x46, 0x34)));

    public AppTileViewModel(
        AppDefinition definition,
        DetectionResult detection,
        ImageSource? icon,
        Action<AppTileViewModel> launch,
        Action<AppTileViewModel> openInstallPage)
    {
        Definition = definition;
        Detection = detection;
        Icon = icon;
        AccentBrush = ParseBrush(definition.Accent) ?? DefaultAccent;

        LaunchCommand = new RelayCommand(_ => launch(this), _ => CanLaunch);
        InstallCommand = new RelayCommand(_ => openInstallPage(this), _ => HasInstallUrl);
    }

    public AppDefinition Definition { get; }

    public DetectionResult Detection { get; }

    public ImageSource? Icon { get; }

    public ICommand LaunchCommand { get; }

    public ICommand InstallCommand { get; }

    public Brush AccentBrush { get; }

    public string Name => Definition.Name;

    public string Description => Definition.Description ?? string.Empty;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Definition.Description);

    /// <summary>Fallback for tiles without an icon.</summary>
    public string Initials
    {
        get
        {
            var words = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                0 => "?",
                1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
                _ => string.Concat(words[0][..1], words[1][..1]).ToUpperInvariant()
            };
        }
    }

    public bool IsInstalled => Detection.Found;

    /// <summary>True when the app is present and there is something concrete to start.</summary>
    public bool CanLaunch => Detection.Found && HasLaunchTarget;

    public bool HasInstallUrl => !Detection.Found && !string.IsNullOrWhiteSpace(Definition.InstallUrl);

    public bool IsWebApp => Definition.Launch?.ResolvedKind == LaunchKind.Uri;

    public string StatusText => Detection.Found
        ? HasLaunchTarget ? IsWebApp ? "Opens in browser" : "Ready" : "Launch not configured"
        : "Not installed";

    public Brush StatusBrush => CanLaunch ? ReadyBrush : MissingBrush;

    /// <summary>Tooltip explaining how the app was found, useful when a tile is unexpectedly greyed out.</summary>
    public string Details => Detection.Found
        ? $"{Name}\nFound by {Detection.Source ?? "configuration"}"
        : $"{Name}\nNot found on this computer";

    private bool HasLaunchTarget
        => Detection.ResolvedPath is not null || !string.IsNullOrWhiteSpace(Definition.Launch?.Target);

    private static Brush? ParseBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            var converted = ColorConverter.ConvertFromString(hex);
            return converted is Color color ? Freeze(new SolidColorBrush(color)) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}

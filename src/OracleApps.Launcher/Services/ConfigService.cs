using System.IO;
using System.Text.Json;
using OracleApps.Launcher.Models;

namespace OracleApps.Launcher.Services;

/// <summary>
/// Loads <c>appsettings.json</c> and <c>config/apps.json</c>. The app list is seeded into the
/// user's profile on first run so it can be edited without touching the installation folder.
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    /// <summary>Per-user folder holding the editable app list and the token cache.</summary>
    public static string UserDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OracleApps");

    /// <summary>The apps.json the launcher actually reads.</summary>
    public string UserConfigPath { get; } = Path.Combine(UserDataDirectory, "apps.json");

    private static string BaseDirectory => AppContext.BaseDirectory;

    /// <summary>Reads sign-in settings shipped next to the executable.</summary>
    public AppSettings LoadSettings()
    {
        // appsettings.local.json lets a machine override the shipped settings without editing them.
        foreach (var name in new[] { "appsettings.local.json", "appsettings.json" })
        {
            var path = Path.Combine(BaseDirectory, name);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
                if (settings is not null)
                {
                    return settings;
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"{name} is not valid JSON: {ex.Message}", ex);
            }
        }

        return new AppSettings();
    }

    /// <summary>Reads the app list, copying the shipped default into the user profile on first run.</summary>
    public LauncherConfig LoadApps()
    {
        var defaultPath = Path.Combine(BaseDirectory, "config", "apps.json");

        if (!File.Exists(UserConfigPath) && File.Exists(defaultPath))
        {
            try
            {
                Directory.CreateDirectory(UserDataDirectory);
                File.Copy(defaultPath, UserConfigPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Fall back to reading the shipped file directly.
            }
        }

        var path = File.Exists(UserConfigPath) ? UserConfigPath : defaultPath;
        if (!File.Exists(path))
        {
            return new LauncherConfig();
        }

        try
        {
            var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(path), JsonOptions)
                         ?? new LauncherConfig();
            config.Apps = config.Apps.Where(a => a.Enabled).ToList();
            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"apps.json is not valid JSON: {ex.Message}", ex);
        }
    }
}

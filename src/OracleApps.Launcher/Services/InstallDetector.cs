using System.IO;
using System.Security;
using Microsoft.Win32;
using OracleApps.Launcher.Models;

namespace OracleApps.Launcher.Services;

/// <summary>Decides whether an app is present on this computer, and where.</summary>
public sealed class InstallDetector
{
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

    /// <summary>Runs every rule for one app and stops at the first hit.</summary>
    public DetectionResult Detect(AppDefinition app)
    {
        var rules = app.Detect;
        if (rules is null || IsEmpty(rules))
        {
            return DetectionResult.AlwaysAvailable;
        }

        // Paths first: a hit there gives us something we can actually start.
        foreach (var pattern in rules.Paths)
        {
            var hit = PathPatterns.ResolveFirst(pattern);
            if (hit is not null)
            {
                return new DetectionResult(true, hit, $"path: {hit}");
            }
        }

        foreach (var executable in rules.Executables)
        {
            var hit = ResolveExecutable(executable);
            if (hit is not null)
            {
                return new DetectionResult(true, hit, $"executable: {hit}");
            }
        }

        foreach (var rule in rules.RegistryValues)
        {
            var value = ReadRegistryValue(rule.Key, rule.Name);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var candidate = value.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(rule.Append))
            {
                candidate = Path.Combine(candidate, rule.Append.TrimStart('\\', '/'));
            }

            // The key can survive an uninstall, so the path it names has to exist too.
            var resolved = PathPatterns.ResolveFirst(candidate);
            if (resolved is null)
            {
                continue;
            }

            return new DetectionResult(true, resolved, $"registry value: {rule.Key}");
        }

        foreach (var key in rules.RegistryKeys)
        {
            if (RegistryKeyExists(key))
            {
                return new DetectionResult(true, null, $"registry key: {key}");
            }
        }

        foreach (var scheme in rules.UriSchemes)
        {
            if (IsUriSchemeRegistered(scheme))
            {
                return new DetectionResult(true, null, $"uri scheme: {scheme}:");
            }
        }

        return DetectionResult.NotFound;
    }

    private static bool IsEmpty(DetectionRules rules)
        => rules.Paths.Count == 0
           && rules.Executables.Count == 0
           && rules.RegistryKeys.Count == 0
           && rules.RegistryValues.Count == 0
           && rules.UriSchemes.Count == 0;

    /// <summary>Finds an executable through PATH and the Windows "App Paths" registry.</summary>
    public static string? ResolveExecutable(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var candidates = Path.HasExtension(name) ? new[] { name } : new[] { name + ".exe", name };

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidate in candidates)
            {
                try
                {
                    var full = Path.Combine(Environment.ExpandEnvironmentVariables(directory.Trim('"')), candidate);
                    if (File.Exists(full))
                    {
                        return full;
                    }
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry.
                }
            }
        }

        foreach (var candidate in candidates)
        {
            var registered = ReadRegistryValue($@"HKLM\{AppPathsKey}\{candidate}", null)
                             ?? ReadRegistryValue($@"HKCU\{AppPathsKey}\{candidate}", null);
            if (!string.IsNullOrWhiteSpace(registered))
            {
                var full = registered.Trim().Trim('"');
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }

    private static bool IsUriSchemeRegistered(string? scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return false;
        }

        var name = scheme.TrimEnd(':', '/');
        return ReadRegistryValue($@"HKCR\{name}", "URL Protocol") is not null
               || ReadRegistryValue($@"HKCU\SOFTWARE\Classes\{name}", "URL Protocol") is not null;
    }

    private static bool RegistryKeyExists(string path)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var key = OpenKey(path, view);
            if (key is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadRegistryValue(string path, string? valueName)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var key = OpenKey(path, view);
            var value = key?.GetValue(string.IsNullOrEmpty(valueName) ? null : valueName);
            if (value is not null)
            {
                return Convert.ToString(value);
            }
        }

        return null;
    }

    private static RegistryKey? OpenKey(string path, RegistryView view)
    {
        var (hive, subKey) = SplitHive(path);
        if (hive is null || string.IsNullOrEmpty(subKey))
        {
            return null;
        }

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive.Value, view);
            return baseKey.OpenSubKey(subKey);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static (RegistryHive? Hive, string SubKey) SplitHive(string path)
    {
        var trimmed = path.Replace('/', '\\').Trim().TrimStart('\\');
        var separator = trimmed.IndexOf('\\');
        if (separator <= 0)
        {
            return (null, string.Empty);
        }

        var hiveName = trimmed[..separator].ToUpperInvariant();
        var subKey = trimmed[(separator + 1)..];

        RegistryHive? hive = hiveName switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
            "HKU" or "HKEY_USERS" => RegistryHive.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => RegistryHive.CurrentConfig,
            _ => null
        };

        return (hive, subKey);
    }
}

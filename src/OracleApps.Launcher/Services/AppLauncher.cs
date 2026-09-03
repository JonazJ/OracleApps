using System.Diagnostics;
using System.IO;
using OracleApps.Launcher.Models;

namespace OracleApps.Launcher.Services;

/// <summary>Starts an app that detection found on this computer.</summary>
public sealed class AppLauncher
{
    /// <summary>Starts <paramref name="app"/>, throwing when there is nothing to start.</summary>
    public void Launch(AppDefinition app, DetectionResult detection)
    {
        var spec = app.Launch ?? new LaunchSpec();

        switch (spec.ResolvedKind)
        {
            case LaunchKind.Uri:
                StartUri(spec.Target, app.Name);
                break;

            case LaunchKind.Executable:
                StartFile(PathPatterns.ResolveFirst(spec.Target) ?? detection.ResolvedPath, spec, app.Name);
                break;

            default:
                var target = detection.ResolvedPath ?? PathPatterns.ResolveFirst(spec.Target);
                if (target is null && !string.IsNullOrWhiteSpace(spec.Target))
                {
                    // Nothing on disk matched, but the target may still be a URI.
                    StartUri(spec.Target, app.Name);
                    return;
                }

                StartFile(target, spec, app.Name);
                break;
        }
    }

    /// <summary>Opens a page in the default browser, used for "not installed" tiles.</summary>
    public void OpenUrl(string url) => StartUri(url, url);

    private static void StartFile(string? target, LaunchSpec spec, string appName)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException(
                $"{appName} has nothing to start. Set \"launch\": {{ \"kind\": \"executable\", \"target\": \"...\" }} in apps.json.");
        }

        if (!File.Exists(target) && !Directory.Exists(target))
        {
            throw new FileNotFoundException($"{appName} could not be started: '{target}' no longer exists.", target);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
            WorkingDirectory = ResolveWorkingDirectory(spec, target)
        };

        if (!string.IsNullOrWhiteSpace(spec.Arguments))
        {
            startInfo.Arguments = Environment.ExpandEnvironmentVariables(spec.Arguments);
        }

        Process.Start(startInfo);
    }

    private static void StartUri(string? target, string appName)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException($"{appName} has no \"target\" to open.");
        }

        var expanded = Environment.ExpandEnvironmentVariables(target).Trim();
        if (!Uri.TryCreate(expanded, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"'{expanded}' is not a valid address for {appName}.");
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private static string? ResolveWorkingDirectory(LaunchSpec spec, string target)
    {
        if (!string.IsNullOrWhiteSpace(spec.WorkingDirectory))
        {
            return Environment.ExpandEnvironmentVariables(spec.WorkingDirectory);
        }

        return Directory.Exists(target) ? target : Path.GetDirectoryName(target);
    }
}

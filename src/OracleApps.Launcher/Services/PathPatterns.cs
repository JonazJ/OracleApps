namespace OracleApps.Launcher.Services;

/// <summary>Expands configured paths: environment variables plus <c>*</c>/<c>?</c> wildcards per segment.</summary>
public static class PathPatterns
{
    private static readonly char[] Separators = { '\\', '/' };

    /// <summary>Returns every existing file or folder matching <paramref name="pattern"/>.</summary>
    public static IReadOnlyList<string> Resolve(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Array.Empty<string>();
        }

        var expanded = Environment.ExpandEnvironmentVariables(pattern).Trim().Trim('"');
        if (expanded.Length == 0)
        {
            return Array.Empty<string>();
        }

        if (!HasWildcard(expanded))
        {
            return File.Exists(expanded) || Directory.Exists(expanded)
                ? new[] { expanded }
                : Array.Empty<string>();
        }

        string root;
        string remainder;
        var detectedRoot = Path.GetPathRoot(expanded);
        if (!string.IsNullOrEmpty(detectedRoot))
        {
            root = detectedRoot;
            remainder = expanded[detectedRoot.Length..];
        }
        else
        {
            root = Directory.GetCurrentDirectory();
            remainder = expanded;
        }

        var segments = remainder.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<string> current = new[] { root };

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var isLast = i == segments.Length - 1;
            var next = new List<string>();

            foreach (var directory in current)
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                try
                {
                    if (HasWildcard(segment))
                    {
                        next.AddRange(isLast
                            ? Directory.EnumerateFileSystemEntries(directory, segment)
                            : Directory.EnumerateDirectories(directory, segment));
                    }
                    else
                    {
                        var combined = Path.Combine(directory, segment);
                        var exists = isLast
                            ? File.Exists(combined) || Directory.Exists(combined)
                            : Directory.Exists(combined);
                        if (exists)
                        {
                            next.Add(combined);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A folder we may not read is simply not a match.
                }
            }

            current = next;
        }

        return current
            .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Returns the first match, or null.</summary>
    public static string? ResolveFirst(string? pattern) => Resolve(pattern).FirstOrDefault();

    private static bool HasWildcard(string value) => value.Contains('*') || value.Contains('?');
}

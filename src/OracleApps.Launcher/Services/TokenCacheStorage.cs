using System.IO;
using System.Security.Cryptography;
using Microsoft.Identity.Client;

namespace OracleApps.Launcher.Services;

/// <summary>
/// Keeps the MSAL token cache on disk, encrypted with DPAPI for the current Windows user, so the
/// launcher can sign the user back in silently the next time it starts.
/// </summary>
public sealed class TokenCacheStorage
{
    private static readonly object Sync = new();
    private readonly string _cacheFile;

    public TokenCacheStorage(string cacheFile) => _cacheFile = cacheFile;

    public void Bind(ITokenCache cache)
    {
        cache.SetBeforeAccess(OnBeforeAccess);
        cache.SetAfterAccess(OnAfterAccess);
    }

    /// <summary>Removes the cached tokens, e.g. on sign-out.</summary>
    public void Clear()
    {
        lock (Sync)
        {
            try
            {
                if (File.Exists(_cacheFile))
                {
                    File.Delete(_cacheFile);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Losing the cache only costs an interactive sign-in next time.
            }
        }
    }

    private void OnBeforeAccess(TokenCacheNotificationArgs args)
    {
        lock (Sync)
        {
            var data = Read();
            if (data is not null)
            {
                args.TokenCache.DeserializeMsalV3(data, shouldClearExistingCache: true);
            }
        }
    }

    private void OnAfterAccess(TokenCacheNotificationArgs args)
    {
        if (!args.HasStateChanged)
        {
            return;
        }

        lock (Sync)
        {
            Write(args.TokenCache.SerializeMsalV3());
        }
    }

    private byte[]? Read()
    {
        try
        {
            if (!File.Exists(_cacheFile))
            {
                return null;
            }

            var protectedBytes = File.ReadAllBytes(_cacheFile);
            return protectedBytes.Length == 0
                ? null
                : ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // Unreadable cache (copied from another machine, corrupted): start over.
            return null;
        }
    }

    private void Write(byte[]? data)
    {
        try
        {
            if (data is null || data.Length == 0)
            {
                Clear();
                return;
            }

            var directory = Path.GetDirectoryName(_cacheFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(_cacheFile, ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // The session still works, it just will not survive a restart.
        }
    }
}

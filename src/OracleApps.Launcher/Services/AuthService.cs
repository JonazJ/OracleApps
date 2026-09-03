using System.IO;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using OracleApps.Launcher.Models;

namespace OracleApps.Launcher.Services;

/// <summary>The result of a successful Microsoft sign-in.</summary>
public sealed record SignInResult(string AccessToken, string DisplayName, string? Username);

/// <summary>Microsoft Entra ID sign-in for the launcher.</summary>
public sealed class AuthService
{
    private readonly AzureAdOptions _options;
    private readonly TokenCacheStorage _cache;
    private IPublicClientApplication? _client;

    public AuthService(AzureAdOptions options)
    {
        _options = options;
        _cache = new TokenCacheStorage(Path.Combine(ConfigService.UserDataDirectory, "msal.cache.bin"));
    }

    /// <summary>Supplies the window handle the Microsoft sign-in dialog is parented to.</summary>
    public Func<IntPtr>? ParentWindowProvider { get; set; }

    /// <summary>Signs in without any UI, using the cached account or the Windows account.</summary>
    /// <returns>Null when the user has to sign in interactively.</returns>
    public async Task<SignInResult?> TrySignInSilentlyAsync(CancellationToken cancellationToken = default)
    {
        var client = GetClient();

        var accounts = await client.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();

        // With the Windows broker the signed-in Windows account can be used without any prompt.
        if (account is null && _options.UseWindowsBroker)
        {
            account = PublicClientApplication.OperatingSystemAccount;
        }

        if (account is null)
        {
            return null;
        }

        try
        {
            var result = await client
                .AcquireTokenSilent(_options.Scopes, account)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);

            return ToSignInResult(result);
        }
        catch (MsalException)
        {
            // Interaction is required, or there is no usable account (for instance because the
            // broker is unavailable on this machine). Either way: fall back to the sign-in card.
            return null;
        }
    }

    /// <summary>Signs in with the Microsoft sign-in dialog.</summary>
    public async Task<SignInResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        var client = GetClient();

        var builder = client.AcquireTokenInteractive(_options.Scopes);

        var handle = ParentWindowProvider?.Invoke() ?? IntPtr.Zero;
        if (handle != IntPtr.Zero)
        {
            builder = builder.WithParentActivityOrWindow(handle);
        }

        var accounts = await client.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();
        builder = account is not null ? builder.WithAccount(account) : builder.WithPrompt(Prompt.SelectAccount);

        var result = await builder.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return ToSignInResult(result);
    }

    /// <summary>Forgets every cached account.</summary>
    public async Task SignOutAsync()
    {
        if (_client is not null)
        {
            foreach (var account in await _client.GetAccountsAsync().ConfigureAwait(false))
            {
                try
                {
                    await _client.RemoveAsync(account).ConfigureAwait(false);
                }
                catch (MsalException)
                {
                    // Keep going: the cache file is deleted below in any case.
                }
            }
        }

        _cache.Clear();
        _client = null;
    }

    private IPublicClientApplication GetClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "Microsoft sign-in is not configured. Set azureAd.clientId in appsettings.json.");
        }

        _client = Build(_options.UseWindowsBroker) ?? Build(useBroker: false)
            ?? throw new InvalidOperationException("The Microsoft sign-in client could not be created.");

        _cache.Bind(_client.UserTokenCache);
        return _client;
    }

    private IPublicClientApplication? Build(bool useBroker)
    {
        try
        {
            var builder = PublicClientApplicationBuilder
                .Create(_options.ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, _options.TenantId)
                .WithClientName("Oracle Apps Launcher")
                .WithDefaultRedirectUri();

            if (useBroker)
            {
                builder = builder.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows));
            }

            return builder.Build();
        }
        catch (Exception ex) when (ex is MsalClientException or DllNotFoundException or TypeInitializationException)
        {
            // Fall back to the browser-based flow when the Windows broker is unavailable.
            return null;
        }
    }

    private static SignInResult ToSignInResult(AuthenticationResult result)
    {
        var name = result.ClaimsPrincipal?.FindFirst("name")?.Value;
        var displayName = !string.IsNullOrWhiteSpace(name)
            ? name
            : result.Account?.Username ?? "Signed in";

        return new SignInResult(result.AccessToken, displayName, result.Account?.Username);
    }
}

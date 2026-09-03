namespace OracleApps.Launcher.Models;

/// <summary>Root of <c>appsettings.json</c>.</summary>
public sealed class AppSettings
{
    public AzureAdOptions AzureAd { get; set; } = new();

    /// <summary>
    /// When true the user may skip Microsoft sign-in and use the launcher locally.
    /// Sign-in is skipped automatically when no client id is configured.
    /// </summary>
    public bool AllowLocalMode { get; set; } = true;
}

/// <summary>Microsoft Entra ID (Azure AD) sign-in settings.</summary>
public sealed class AzureAdOptions
{
    /// <summary>Application (client) id of the Entra app registration. Empty disables SSO.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Tenant id, or <c>organizations</c> / <c>common</c>.</summary>
    public string TenantId { get; set; } = "organizations";

    /// <summary>Delegated scopes requested at sign-in.</summary>
    public List<string> Scopes { get; set; } = new() { "User.Read" };

    /// <summary>Use the Windows account broker (WAM) so the signed-in Windows account is reused.</summary>
    public bool UseWindowsBroker { get; set; } = true;

    /// <summary>Whether the signed-in user's photo and name are read from Microsoft Graph.</summary>
    public bool LoadProfileFromGraph { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}

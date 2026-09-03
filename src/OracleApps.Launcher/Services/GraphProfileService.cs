using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OracleApps.Launcher.Models;

namespace OracleApps.Launcher.Services;

/// <summary>Reads the signed-in user's name and picture from Microsoft Graph.</summary>
public sealed class GraphProfileService : IDisposable
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>Returns the profile, falling back to what sign-in already told us.</summary>
    public async Task<UserProfile> GetProfileAsync(SignInResult signIn, CancellationToken cancellationToken = default)
    {
        var displayName = signIn.DisplayName;
        var email = signIn.Username;

        try
        {
            using var request = CreateRequest(
                HttpMethod.Get,
                $"{GraphBaseUrl}/me?$select=displayName,mail,userPrincipalName",
                signIn.AccessToken);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var root = document.RootElement;

                displayName = ReadString(root, "displayName") ?? displayName;
                email = ReadString(root, "mail") ?? ReadString(root, "userPrincipalName") ?? email;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // The launcher works fine without Graph; keep the claims from the token.
        }

        return new UserProfile
        {
            DisplayName = displayName,
            Email = email,
            Photo = await TryGetPhotoAsync(signIn.AccessToken, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<ImageSource?> TryGetPhotoAsync(string accessToken, CancellationToken cancellationToken)
    {
        foreach (var url in new[] { $"{GraphBaseUrl}/me/photos/96x96/$value", $"{GraphBaseUrl}/me/photo/$value" })
        {
            try
            {
                using var request = CreateRequest(HttpMethod.Get, url, accessToken);
                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
                {
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                if (bytes.Length == 0)
                {
                    continue;
                }

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = new MemoryStream(bytes);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
            {
                // No picture is not an error.
            }
        }

        return null;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public void Dispose() => _http.Dispose();
}

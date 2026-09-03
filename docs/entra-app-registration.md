# Registering the launcher in Microsoft Entra ID

The launcher signs users in as a **public client** (a desktop app with no secret). These steps take
about five minutes in the Azure portal.

## 1. Create the app registration

1. Go to **Entra ID → App registrations → New registration**.
2. Name: `Oracle Apps Launcher`.
3. Supported account types: **Accounts in this organizational directory only** (single tenant) unless
   several tenants need it.
4. Leave the redirect URI empty for now and select **Register**.
5. Copy the **Application (client) ID** and **Directory (tenant) ID** from the overview page.

## 2. Add the redirect URIs

Under **Authentication → Add a platform → Mobile and desktop applications**, tick or add:

| Redirect URI | Needed for |
| --- | --- |
| `https://login.microsoftonline.com/common/oauth2/nativeclient` | the browser sign-in fallback |
| `http://localhost` | the browser sign-in fallback |
| `ms-appx-web://microsoft.aad.brokerplugin/<client-id>` | the Windows account broker (`useWindowsBroker: true`) |

Replace `<client-id>` with the Application (client) ID.

Under **Advanced settings**, make sure **Allow public client flows** is set to **Yes**.

## 3. Permissions

**API permissions → Add a permission → Microsoft Graph → Delegated permissions → `User.Read`**, then
**Grant admin consent** if your tenant requires it. `User.Read` is only used to show the signed-in
person's name and photo in the header; set `loadProfileFromGraph: false` in `appsettings.json` to
skip that call.

## 4. Fill in appsettings.json

```json
{
  "azureAd": {
    "clientId": "<Application (client) ID>",
    "tenantId": "<Directory (tenant) ID>",
    "scopes": [ "User.Read" ],
    "useWindowsBroker": true,
    "loadProfileFromGraph": true
  },
  "allowLocalMode": false
}
```

`tenantId` also accepts `organizations` (any work or school account) or `common`. Set
`allowLocalMode: false` once sign-in works, so the launcher cannot be used without signing in.

## 5. Restricting who may open the launcher

Sign-in on its own lets in anyone in the tenant. To limit it to a group of users, set
**Enterprise applications → Oracle Apps Launcher → Properties → Assignment required?** to **Yes**,
then assign the users or groups under **Users and groups**. Everyone else gets an
`AADSTS50105` error at sign-in instead of the app window.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| `AADSTS50011: redirect URI does not match` | The URI from step 2 is missing — the message names the one it wanted. |
| `AADSTS7000218` | "Allow public client flows" is still **No**. |
| Sign-in window never appears | The broker is unavailable; the launcher falls back to the browser flow, which needs `http://localhost` registered. |
| Name shows as an email address | Graph could not be reached, or `User.Read` has not been consented. |

# Oracle Apps Launcher

A small Windows desktop client that gathers 4–6 apps in one window, so they do not all have to sit
in the taskbar. It signs the user in with Microsoft (Entra ID) single sign-on, looks for the
configured apps on the computer, and presents whatever is installed as tiles on a background
picture. Clicking a tile starts the app.

```
┌───────────────────────────────────────────────────────────┐
│  Oracle Apps                              ( ) Jonas J     │
│  Everything in one window                                 │
│                                                           │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐       │
│  │ ▣ SQL Dev    │ │ ▣ VirtualBox │ │ ▣ Primavera  │       │
│  │ ● Ready      │ │ ● Ready      │ │ ● Not inst.  │       │
│  └──────────────┘ └──────────────┘ └──────────────┘       │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐       │
│  │ ▣ EBS        │ │ ▣ APEX       │ │ ▣ Analytics  │       │
│  │ ● Browser    │ │ ● Browser    │ │ ● Browser    │       │
│  └──────────────┘ └──────────────┘ └──────────────┘       │
└───────────────────────────────────────────────────────────┘
```

## Requirements

- Windows 10 1809 or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build (users only need the
  .NET 8 Desktop Runtime, or use a self-contained publish)

## Run it

```powershell
dotnet run --project src\OracleApps.Launcher
```

Out of the box no client id is configured, so the launcher starts straight in **local mode**: it
skips sign-in and shows the apps it finds on the computer. That is the quickest way to see it work.

## Publish an .exe

```powershell
# needs the .NET 8 Desktop Runtime on the target machine
dotnet publish src\OracleApps.Launcher -c Release -r win-x64 --self-contained false -o publish

# or a single file that carries the runtime with it
dotnet publish src\OracleApps.Launcher -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish
```

## Microsoft sign-in

1. Register an app in Entra ID (Azure AD) — the steps are in
   [docs/entra-app-registration.md](docs/entra-app-registration.md).
2. Put the ids in `appsettings.json` next to the executable:

```json
{
  "azureAd": {
    "clientId": "00000000-0000-0000-0000-000000000000",
    "tenantId": "contoso.onmicrosoft.com",
    "scopes": [ "User.Read" ],
    "useWindowsBroker": true,
    "loadProfileFromGraph": true
  },
  "allowLocalMode": true
}
```

- `useWindowsBroker` reuses the account the user is already signed in to Windows with, so most
  people never see a prompt at all.
- Tokens are cached in `%APPDATA%\OracleApps\msal.cache.bin`, encrypted with DPAPI for that Windows
  user, so the next start signs in silently.
- `allowLocalMode: false` removes the "Continue without signing in" option and makes sign-in
  mandatory.
- `appsettings.local.json`, if present, is read instead of `appsettings.json`. It is git-ignored, so
  it is the right place for a personal tenant while testing.

## The app list

The app list lives in `config\apps.json` and is copied to `%APPDATA%\OracleApps\apps.json` the first
time the launcher runs. That copy is the one it reads afterwards, and the **Edit app list** button in
the footer opens it. Press **Refresh** after saving.

```jsonc
{
  "id": "sql-developer",
  "name": "SQL Developer",
  "description": "Browse schemas and run queries.",
  "accent": "#2E7DD1",
  "iconPath": "",                       // optional .ico/.png; otherwise the app's own icon is used
  "detect": { "paths": [ "%ProgramFiles%\\sqldeveloper\\sqldeveloper.exe" ] },
  "launch": { "kind": "detected" },
  "installUrl": "https://example.com/download",
  "enabled": true
}
```

### How an app is found

`detect` may hold any mix of these; the first hit wins, and a hit that resolves to a file also
becomes what the tile starts:

| Rule | What it does |
| --- | --- |
| `paths` | Files or folders. `%ENV%` variables are expanded and `*` / `?` wildcards work per segment, e.g. `...\Primavera P6\P6 Professional\*\PM.exe`. |
| `executables` | Names looked up in `PATH` and in the Windows *App Paths* registry, e.g. `sqldeveloper.exe`. |
| `registryValues` | `{ "key": "HKLM\\SOFTWARE\\Oracle\\VirtualBox", "name": "InstallDir", "append": "VirtualBox.exe" }` — the value is used as the install path, and has to point at something that exists. |
| `registryKeys` | The key only has to exist. Both the 64-bit and 32-bit registry views are checked. |
| `uriSchemes` | A registered protocol such as `ms-excel`. |

Leave `detect` out entirely for a web app: with no rules the tile is always available.

### How a tile starts its app

| `launch.kind` | Behaviour |
| --- | --- |
| `detected` (default) | Starts whatever detection resolved. Falls back to `target` if detection found nothing on disk. |
| `executable` | Starts `target`, with optional `arguments` and `workingDirectory`. |
| `uri` | Hands `target` to the shell, e.g. `https://ebs.example.com` or a custom `myapp://` protocol. |

A tile is greyed out when the app is not installed. If `installUrl` is set it also shows a
**Get the app** link. Hover any tile to see how it was found — useful when one is unexpectedly grey.

### Background picture

Set `backgroundImage` to a jpg/png path (`%ENV%` variables allowed) to use your own; the built-in
gradient in `src/OracleApps.Launcher/Assets/background.png` is the fallback.

## Layout

```
src/OracleApps.Launcher/
├─ MainWindow.xaml           window: background, header, tile grid, sign-in overlay
├─ Themes/Styles.xaml        colours, buttons, tile chrome
├─ ViewModels/               MainViewModel (sign-in + detection), AppTileViewModel (one tile)
├─ Services/
│  ├─ AuthService.cs         MSAL sign-in, Windows broker, silent re-sign-in
│  ├─ TokenCacheStorage.cs   DPAPI-encrypted token cache
│  ├─ GraphProfileService.cs name and photo from Microsoft Graph
│  ├─ InstallDetector.cs     registry / path / PATH / protocol detection
│  ├─ PathPatterns.cs        %ENV% and wildcard expansion
│  ├─ AppLauncher.cs         starts the app or opens the URL
│  └─ ConfigService.cs       appsettings.json and apps.json
├─ config/apps.json          the app list shipped as the default
└─ appsettings.json          sign-in settings
```

## Notes

- Sign-in identifies the user; which tiles are enabled is decided by what is installed on the
  computer. If you later want entitlements to come from Entra instead (app roles or group
  membership), that check belongs next to `InstallDetector` in `MainViewModel.Scan`.
- `apps.json` is read from the user's own profile and can start any program that user could start
  anyway; it is not a security boundary.

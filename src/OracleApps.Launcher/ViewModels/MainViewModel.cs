using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Identity.Client;
using OracleApps.Launcher.Common;
using OracleApps.Launcher.Models;
using OracleApps.Launcher.Services;

namespace OracleApps.Launcher.ViewModels;

/// <summary>Drives the launcher window: sign-in, app detection and starting apps.</summary>
public sealed class MainViewModel : ObservableObject
{
    private const string PackedBackground = "pack://application:,,,/Assets/background.png";

    private readonly ConfigService _configService = new();
    private readonly InstallDetector _detector = new();
    private readonly AppLauncher _appLauncher = new();
    private readonly GraphProfileService _graph = new();

    private AppSettings _settings = new();
    private LauncherConfig _config = new();
    private AuthService? _auth;

    private string _title = "Oracle Apps";
    private string _subtitle = "All your apps in one place";
    private string _statusMessage = string.Empty;
    private string _signInMessage = "Sign in with your work or school account.";
    private bool _isStatusError;
    private bool _isSignedIn;
    private bool _isBusy;
    private UserProfile? _user;
    private ImageSource? _backgroundImage;

    public MainViewModel()
    {
        SignInCommand = new AsyncRelayCommand(_ => SignInAsync(), _ => !IsSignedIn && IsSsoConfigured);
        ContinueLocallyCommand = new AsyncRelayCommand(
            _ => EnterLocalModeAsync("Running in local mode — apps are found by looking at this computer."),
            _ => !IsSignedIn && _settings.AllowLocalMode);
        SignOutCommand = new AsyncRelayCommand(_ => SignOutAsync(), _ => IsSignedIn);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAppsAsync(), _ => IsSignedIn && !IsBusy);
        EditConfigCommand = new RelayCommand(_ => OpenConfigFile());
    }

    /// <summary>Supplies the launcher window handle to the Microsoft sign-in dialog.</summary>
    public Func<IntPtr>? WindowHandleProvider { get; set; }

    public ObservableCollection<AppTileViewModel> Apps { get; } = new();

    public ICommand SignInCommand { get; }

    public ICommand ContinueLocallyCommand { get; }

    public ICommand SignOutCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand EditConfigCommand { get; }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    /// <summary>Message shown on the sign-in card.</summary>
    public string SignInMessage
    {
        get => _signInMessage;
        private set => SetProperty(ref _signInMessage, value);
    }

    public bool IsSignedIn
    {
        get => _isSignedIn;
        private set
        {
            if (SetProperty(ref _isSignedIn, value))
            {
                OnPropertyChanged(nameof(ShowSignIn));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool ShowSignIn => !IsSignedIn;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public UserProfile? User
    {
        get => _user;
        private set => SetProperty(ref _user, value);
    }

    public ImageSource? BackgroundImage
    {
        get => _backgroundImage;
        private set => SetProperty(ref _backgroundImage, value);
    }

    public bool IsSsoConfigured => _settings.AzureAd.IsConfigured;

    public bool AllowLocalMode => _settings.AllowLocalMode;

    /// <summary>Loads configuration and tries to sign the user in without prompting.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            _settings = _configService.LoadSettings();
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(ex.Message, isError: true);
        }

        try
        {
            _config = _configService.LoadApps();
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(ex.Message, isError: true);
        }

        Title = _config.Title;
        Subtitle = _config.Subtitle;
        BackgroundImage = LoadBackground(_config.BackgroundImage);
        OnPropertyChanged(nameof(IsSsoConfigured));
        OnPropertyChanged(nameof(AllowLocalMode));

        if (!IsSsoConfigured)
        {
            await EnterLocalModeAsync(
                "Microsoft sign-in is not configured yet — set azureAd.clientId in appsettings.json.");
            return;
        }

        _auth = new AuthService(_settings.AzureAd) { ParentWindowProvider = WindowHandleProvider };

        IsBusy = true;
        SignInMessage = "Checking your Microsoft account…";
        try
        {
            var silent = await _auth.TrySignInSilentlyAsync().ConfigureAwait(true);
            if (silent is not null)
            {
                await CompleteSignInAsync(silent).ConfigureAwait(true);
                return;
            }

            SignInMessage = "Sign in with your work or school account.";
        }
        catch (MsalException ex)
        {
            SignInMessage = $"Sign-in is not available right now: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignInAsync()
    {
        if (_auth is null)
        {
            return;
        }

        IsBusy = true;
        SignInMessage = "Waiting for the Microsoft sign-in window…";
        try
        {
            var result = await _auth.SignInAsync().ConfigureAwait(true);
            await CompleteSignInAsync(result).ConfigureAwait(true);
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            SignInMessage = "Sign-in was cancelled.";
        }
        catch (MsalException ex)
        {
            SignInMessage = $"Sign-in failed: {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            SignInMessage = "Sign-in was cancelled.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CompleteSignInAsync(SignInResult signIn)
    {
        User = _settings.AzureAd.LoadProfileFromGraph
            ? await _graph.GetProfileAsync(signIn).ConfigureAwait(true)
            : new UserProfile { DisplayName = signIn.DisplayName, Email = signIn.Username };

        IsSignedIn = true;
        await RefreshAppsAsync().ConfigureAwait(true);
    }

    private async Task EnterLocalModeAsync(string message)
    {
        User = new UserProfile { DisplayName = Environment.UserName, IsLocalMode = true };
        IsSignedIn = true;
        await RefreshAppsAsync().ConfigureAwait(true);
        SetStatus(message);
    }

    private async Task SignOutAsync()
    {
        if (_auth is not null)
        {
            await _auth.SignOutAsync().ConfigureAwait(true);
        }

        Apps.Clear();
        User = null;
        IsSignedIn = false;
        StatusMessage = string.Empty;
        SignInMessage = "Signed out. Sign in again to see your apps.";
    }

    /// <summary>Looks for every configured app on this computer and rebuilds the tiles.</summary>
    private async Task RefreshAppsAsync()
    {
        IsBusy = true;
        SetStatus("Looking for your apps on this computer…");

        var definitions = _config.Apps.ToList();
        var scanned = await Task.Run(() => definitions.Select(Scan).ToList()).ConfigureAwait(true);

        Apps.Clear();
        foreach (var (definition, detection, icon) in scanned)
        {
            Apps.Add(new AppTileViewModel(definition, detection, icon, LaunchApp, OpenInstallPage));
        }

        IsBusy = false;

        var available = Apps.Count(a => a.CanLaunch);
        SetStatus(Apps.Count == 0
            ? $"No apps configured yet. Add them to {_configService.UserConfigPath}."
            : $"{available} of {Apps.Count} apps ready on this computer.");
    }

    private (AppDefinition Definition, DetectionResult Detection, ImageSource? Icon) Scan(AppDefinition definition)
    {
        var detection = _detector.Detect(definition);
        var iconSource = detection.ResolvedPath ?? PathPatterns.ResolveFirst(definition.Launch?.Target);
        return (definition, detection, IconLoader.Load(definition.IconPath, iconSource));
    }

    private void LaunchApp(AppTileViewModel tile)
    {
        try
        {
            _appLauncher.Launch(tile.Definition, tile.Detection);
            SetStatus($"Starting {tile.Name}…");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private void OpenInstallPage(AppTileViewModel tile)
    {
        if (string.IsNullOrWhiteSpace(tile.Definition.InstallUrl))
        {
            return;
        }

        try
        {
            _appLauncher.OpenUrl(tile.Definition.InstallUrl);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private void OpenConfigFile()
    {
        var path = File.Exists(_configService.UserConfigPath)
            ? _configService.UserConfigPath
            : ConfigService.UserDataDirectory;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            SetStatus($"Could not open {path}: {ex.Message}", isError: true);
        }
    }

    private static ImageSource? LoadBackground(string? configuredPath)
    {
        var fromConfig = PathPatterns.ResolveFirst(configuredPath);
        return (fromConfig is not null ? TryLoadImage(fromConfig) : null) ?? TryLoadImage(PackedBackground);
    }

    private static ImageSource? TryLoadImage(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(Path.IsPathRooted(path) ? Path.GetFullPath(path) : path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or FormatException)
        {
            // No background picture: the window falls back to its gradient.
            return null;
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        IsStatusError = isError;
        StatusMessage = message;
    }
}

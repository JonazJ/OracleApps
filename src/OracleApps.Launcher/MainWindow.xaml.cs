using System.Windows;
using System.Windows.Interop;
using OracleApps.Launcher.ViewModels;

namespace OracleApps.Launcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();

        _viewModel.WindowHandleProvider = () => new WindowInteropHelper(this).Handle;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// A maximised WindowChrome window reaches past the work area by its resize border, which would
    /// clip the header and the caption buttons. Pull the content back in by the same amount.
    /// </summary>
    private void OnStateChanged(object? sender, EventArgs e)
        => RootLayout.Margin = WindowState == WindowState.Maximized
            ? SystemParameters.WindowResizeBorderThickness
            : default;

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

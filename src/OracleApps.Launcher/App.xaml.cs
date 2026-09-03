using System.Windows;
using System.Windows.Threading;

namespace OracleApps.Launcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        new MainWindow().Show();
    }

    /// <summary>Keeps an unexpected error from closing the launcher without a word.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.Message,
            "Oracle Apps Launcher",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }
}

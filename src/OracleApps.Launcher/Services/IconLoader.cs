using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OracleApps.Launcher.Services;

/// <summary>Produces tile icons: a configured image if there is one, otherwise the app's own icon.</summary>
public static class IconLoader
{
    /// <summary>Returns a frozen image, or null when no icon can be produced.</summary>
    public static ImageSource? Load(string? iconPath, string? executablePath)
    {
        var configured = PathPatterns.ResolveFirst(iconPath);
        if (configured is not null && File.Exists(configured))
        {
            var fromFile = LoadFromFile(configured);
            if (fromFile is not null)
            {
                return fromFile;
            }
        }

        return executablePath is not null ? ExtractFromExecutable(executablePath) : null;
    }

    private static ImageSource? LoadFromFile(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or ArgumentException)
        {
            return null;
        }
    }

    private static ImageSource? ExtractFromExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var extension = Path.GetExtension(path);
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}

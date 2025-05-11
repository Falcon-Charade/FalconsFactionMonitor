using MaterialDesignThemes.Wpf;
using System.Windows;
using FalconsFactionMonitor.Themes;

namespace FalconsFactionMonitor.Windows
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var paletteHelper = new PaletteHelper();
            ITheme theme = paletteHelper.GetTheme();

            var (savedTheme, primary, secondary) = AppTheme.LoadThemeFromRegistry();
            if (savedTheme != null && primary.HasValue && secondary.HasValue)
            {
                var userTheme = AppTheme.Create(savedTheme.Value, primary.Value, secondary.Value);
                paletteHelper.SetTheme(userTheme);
                return;
            }

            // Fall back to system theme if no saved theme exists
            var systemTheme = AppTheme.GetSystemTheme();
            var primarycolor = AppTheme.GetPrimaryColor(systemTheme ?? BaseTheme.Light);
            var accent = AppTheme.GetSystemAccentColor();
            if (systemTheme != null && primarycolor.HasValue && accent.HasValue)
            {
                var fallbackTheme = AppTheme.Create(systemTheme.Value, (System.Windows.Media.Color)primarycolor, accent.Value);
                paletteHelper.SetTheme(fallbackTheme);
            }
        }
    }
}
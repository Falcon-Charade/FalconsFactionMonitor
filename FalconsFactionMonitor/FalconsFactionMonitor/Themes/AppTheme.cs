using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows.Media;

namespace FalconsFactionMonitor.Themes;

/// <summary>
/// Based partly on ControlzEx
/// https://github.com/ControlzEx/ControlzEx/blob/48230bb023c588e1b7eb86ea83f7ddf7d25be735/src/ControlzEx/Theming/WindowsThemeHelper.cs
/// </summary>

public partial class AppTheme
{
    /// The registry path for the theme settings
    private const string RegistryPath = @"HKEY_CURRENT_USER\Software\FalconCharade\FalconsFactionMonitor\Theme";

    //Get System Light/Dark Theme
    public static BaseTheme? GetSystemTheme()
    {
        try
        {
            var registryValue = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", null);

            if (registryValue is null)
            {
                return null;
            }

            return Convert.ToBoolean(registryValue) ? BaseTheme.Light : BaseTheme.Dark;
        }
        catch (Exception)
        {
            return null;
        }
    }

    //Set Primary Color Based on System Theme
    public static Color? GetPrimaryColor(BaseTheme baseTheme)
    {
        Color? primaryColor;
        if (baseTheme == BaseTheme.Light)
        {
            primaryColor = (Color)ColorConverter.ConvertFromString("#7B1FA2"); // Material Design Deep Purple 700
        }
        else
        {
            primaryColor = (Color)ColorConverter.ConvertFromString("#64B5F6"); // Material Design Light Blue 300
        }
        return primaryColor;
    }

    //Get System Accent Color
    public static Color? GetSystemAccentColor()
    {
        Color? accentColor = null;

        try
        {
            var registryValue = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "ColorizationColor", null);

            if (registryValue is null)
            {
                return null;
            }

            // We get negative values out of the registry, so we have to cast to int from object first.
            // Casting from int to uint works afterwards and converts the number correctly.
            var pp = (uint)(int)registryValue;
            if (pp > 0)
            {
                var bytes = BitConverter.GetBytes(pp);
                accentColor = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
            }

            return accentColor;
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }

        return accentColor;
    }

    // Creates a new Theme instance with the specified base theme, primary color, and secondary color.
    public static Theme Create(BaseTheme baseTheme, Color primary, Color secondary)
    {
        Theme theme = new();

        theme.SetBaseTheme(baseTheme == BaseTheme.Light ? Theme.Light : Theme.Dark);
        theme.SetPrimaryColor(primary);
        theme.SetSecondaryColor(secondary);

        return theme;
    }

    // Saves the theme settings to the registry.
    public static void SaveThemeToRegistry(BaseTheme baseTheme, Color primary, Color secondary)
    {
        Registry.SetValue(RegistryPath, "BaseTheme", baseTheme.ToString());
        Registry.SetValue(RegistryPath, "PrimaryColor", primary.ToString()); // "#FFxxxxxx"
        Registry.SetValue(RegistryPath, "SecondaryColor", secondary.ToString());
    }
    // Loads the theme settings from the registry.
    public static (BaseTheme? baseTheme, Color? primary, Color? secondary) LoadThemeFromRegistry()
    {
        try
        {
            var baseThemeStr = Registry.GetValue(RegistryPath, "BaseTheme", null)?.ToString();
            var primaryStr = Registry.GetValue(RegistryPath, "PrimaryColor", null)?.ToString();
            var secondaryStr = Registry.GetValue(RegistryPath, "SecondaryColor", null)?.ToString();

            if (baseThemeStr is null || primaryStr is null || secondaryStr is null)
                return (null, null, null);

            var baseTheme = (BaseTheme)Enum.Parse(typeof(BaseTheme), baseThemeStr);
            var primary = (Color)ColorConverter.ConvertFromString(primaryStr);
            var secondary = (Color)ColorConverter.ConvertFromString(secondaryStr);

            return (baseTheme, primary, secondary);
        }
        catch
        {
            return (null, null, null);
        }
    }

    public ColorAdjustment? ColorAdjustment { get; set; }

    public ColorPair SecondaryLight { get; set; }
    public ColorPair SecondaryMid { get; set; }
    public ColorPair SecondaryDark { get; set; }

    public ColorPair PrimaryLight { get; set; }
    public ColorPair PrimaryMid { get; set; }
    public ColorPair PrimaryDark { get; set; }
}
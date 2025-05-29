using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MaterialDesignColors;
using System.Windows.Media;
using Microsoft.Win32;

namespace FalconsFactionMonitor.Helpers
{
    public static class LanguageHelper
    {
        private const string RegistryPath = @"Software\FalconCharade\FalconsFactionMonitor";
        private const string LanguageKey = "Language";

        public static string GetLanguageFromRegistry()
        {
            var value = Registry.CurrentUser.OpenSubKey(RegistryPath)?.GetValue(LanguageKey) as string;
            return string.IsNullOrEmpty(value)
                ? Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName
                : value;
        }

        public static void SetLanguageToRegistry(string languageCode)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue(LanguageKey, languageCode, RegistryValueKind.String);
        }

        public static void SetLanguage(string cultureCode)
        {
            // ① Apply thread cultures
            var culture = new CultureInfo(cultureCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // ② Build a pack URI to the embedded ResourceDictionary
            const string asm = "FalconsFactionMonitor";
            var packPath = $"pack://application:,,,/{asm};component/Resources/StringResources.{cultureCode}.xaml";
            var uri = new Uri(packPath, UriKind.Absolute);

            // ③ Remove any previously‐merged StringResources.*.xaml
            var oldDicts = Application.Current.Resources.MergedDictionaries
                .Where(d => d.Source?.OriginalString.Contains("StringResources.") == true)
                .ToList();
            foreach (var d in oldDicts)
                Application.Current.Resources.MergedDictionaries.Remove(d);

            // ④ Merge in the new ResourceDictionary
            var dict = new ResourceDictionary { Source = uri };
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }
        public static void RefreshDynamicResources(DependencyObject parent)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<FrameworkElement>())
            {
                var keys = child.Resources.Keys.Cast<object>().ToList();

                foreach (var key in keys)
                {
                    if (key is string && child.TryFindResource(key) is object newValue)
                    {
                        if (child is ContentControl contentControl)
                            contentControl.Content = newValue;
                    }
                }

                RefreshDynamicResources(child);
            }
        }
    }
    public class FontSizeOffsetConverter : IValueConverter
    {
        public double Offset { get; set; } = 4;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double baseSize)
                return baseSize + Offset;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
    class LocalizedSwatch
    {
        private readonly Swatch _swatch;
        private readonly Color? _color;
        private readonly string _resourceKey;

        public string DisplayName
        {
            get
            {
                // try to pull from resources first
                var localized = Application.Current.TryFindResource(_resourceKey) as string;
                if (!string.IsNullOrEmpty(localized))
                    return localized;

                // fallback to English name
                if (_swatch != null)
                    return _swatch.Name;
                if (_color.HasValue)
                    return _color.Value.ToString();
                return string.Empty;
            }
        }

        public Brush PreviewBrush
        {
            get
            {
                if (_swatch != null)
                    return new SolidColorBrush(_swatch.ExemplarHue.Color);
                return new SolidColorBrush(_color.Value);
            }
        }

        // ◆ existing constructor for material swatches
        public LocalizedSwatch(Swatch s)
        {
            _swatch = s;
            _color = null;
            _resourceKey = $"Options_Custom_ColorSwatch_{s.Name.Replace(" ", "")}";
        }

        // ◆ new constructor for manual colors (Pure Black / Pure White)
        public LocalizedSwatch(Color color, string resourceKey)
        {
            _swatch = null;
            _color = color;
            _resourceKey = resourceKey;
        }
    }

}

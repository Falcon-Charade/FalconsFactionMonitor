using System.Windows.Media;
using MaterialDesignColors;

namespace FalconsFactionMonitor.Windows
{
    /// <summary>
    /// Wraps either a plain Color (e.g., Pure Black/White) or a MaterialDesign Swatch,
    /// and optionally carries a resource key for localized display.
    /// </summary>
    public sealed class LocalizedSwatch
    {
        public LocalizedSwatch(Color color, string displayNameKey = null)
        {
            _primaryColor = color;
            DisplayNameKey = displayNameKey;
        }

        public LocalizedSwatch(Swatch swatch)
        {
            Swatch = swatch;
        }

        public Swatch Swatch { get; }
        public string DisplayNameKey { get; }

        public Color PrimaryColor => Swatch?.ExemplarHue.Color ?? _primaryColor;
        public Color? AccentColor => Swatch?.AccentExemplarHue?.Color;

        private readonly Color _primaryColor;

        // ▼ Back-compat for existing XAML/DataTemplates:
        //    If your ItemTemplate uses these (DisplayName/PreviewBrush/Underlying),
        //    they will now work without changing XAML.
        public string DisplayName
        {
            get
            {
                // explicit key wins (e.g., Pure Black/White)
                if (!string.IsNullOrWhiteSpace(DisplayNameKey))
                {
                    var fromRes = System.Windows.Application.Current.TryFindResource(DisplayNameKey) as string;
                    if (!string.IsNullOrWhiteSpace(fromRes))
                        return fromRes;
                }
                // swatch-based localization key
                if (Swatch is Swatch s)
                {
                    var key = $"Options_Custom_ColorSwatch_{s.Name.Replace(" ", string.Empty)}";
                    return (System.Windows.Application.Current.TryFindResource(key) as string) ?? s.Name;
                }
                // fallback to color string
                return PrimaryColor.ToString();
            }
        }

        public Brush PreviewBrush => new SolidColorBrush(PrimaryColor);

        public object Underlying => (object)Swatch ?? (object)PrimaryColor;
    }
}
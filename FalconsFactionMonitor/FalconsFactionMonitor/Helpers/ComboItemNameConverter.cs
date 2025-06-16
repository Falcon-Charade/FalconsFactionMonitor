using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignColors;

namespace FalconsFactionMonitor.Windows
{
    public class ComboItemNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 1) a System.Windows.Media.Color
            if (value is Color c)
            {
                // pick the right resource key for black/white
                var key = c == Colors.Black
                          ? "Options_Custom_PrimaryColor_PureBlack"
                          : c == Colors.White
                            ? "Options_Custom_PrimaryColor_PureWhite"
                            : null;
                if (key != null)
                    return Application.Current.TryFindResource(key) as string;
                return c.ToString();
            }
            // 2) a MaterialDesign Swatch
            else if (value is Swatch sw)
            {
                var key = $"Options_Custom_ColorSwatch_{sw.Name.Replace(" ", "")}";
                return Application.Current.TryFindResource(key) as string
                       ?? sw.Name;
            }
            return string.Empty;
        }



        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

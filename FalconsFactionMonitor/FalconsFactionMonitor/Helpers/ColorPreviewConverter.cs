using System;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignColors;

namespace FalconsFactionMonitor.Windows
{
    public class ColorPreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                ComboBoxItem item when item.Tag is Color color => color,
                Swatch swatch when swatch.ExemplarHue != null => swatch.ExemplarHue.Color,
                Swatch swatch => swatch.PrimaryHues.FirstOrDefault()?.Color ?? Colors.Transparent,
                _ => Colors.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
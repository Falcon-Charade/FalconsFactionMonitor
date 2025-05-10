using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using MaterialDesignColors;

namespace FalconsFactionMonitor.Windows
{
    public class ComboItemNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                ComboBoxItem item => item.Content?.ToString(),
                Swatch swatch => swatch.Name,
                _ => string.Empty
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignColors;

namespace FalconsFactionMonitor.Windows
{
    public class ColorPreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LocalizedSwatch ls)
                return new SolidColorBrush(ls.PrimaryColor);
            if (value is Color c)
                return new SolidColorBrush(c);
            if (value is Swatch sw)
                return new SolidColorBrush(sw.ExemplarHue.Color);
            return DependencyProperty.UnsetValue;
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
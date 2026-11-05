using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Samba.Presentation.Controls.Converters
{
    // Keep configured payment labels readable without changing their stored settings.
    public class PaymentFontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double size;
            if (value == null || !double.TryParse(value.ToString(), out size) || size <= 0) return 22d;
            return Math.Max(18d, Math.Min(30d, size));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class NullBrushConverter : IValueConverter
    {
        private readonly BrushConverter _brushConverter = new BrushConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.ToString() == "")
                return DependencyProperty.UnsetValue;
            return _brushConverter.ConvertFromString(value.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

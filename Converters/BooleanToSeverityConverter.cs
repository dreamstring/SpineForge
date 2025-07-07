using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace SpineForge.Converters
{
    public class BooleanToSeverityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            }
            return InfoBarSeverity.Informational;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
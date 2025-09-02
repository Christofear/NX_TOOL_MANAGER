using System.Globalization;
using System.Windows.Data;

namespace NX_TOOL_MANAGER.Converters
{
    public class PageIsSelectedConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
            => value != null && parameter != null && value.Equals(parameter);

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null) return parameter;
            return Binding.DoNothing;
        }
    }
}

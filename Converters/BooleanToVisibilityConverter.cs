using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NX_TOOL_MANAGER
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString() == "Invert";
            bool flag = value is bool b && b;
            if (invert) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                bool invert = parameter?.ToString() == "Invert";
                bool result = v == Visibility.Visible;
                return invert ? !result : result;
            }
            return false;
        }
    }
}
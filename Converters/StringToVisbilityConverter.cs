using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NX_TOOL_MANAGER.Converters
{
    /// <summary>
    /// Converts a string to a Visibility value.
    /// Returns Visibility.Visible if the string is not null or empty.
    /// Returns Visibility.Collapsed if the string is null or empty.
    /// This is useful for showing or hiding UI elements based on whether a TextBox has content.
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Check if the incoming value is a non-empty string.
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter is only used for one-way binding, so ConvertBack is not needed.
            throw new NotImplementedException();
        }
    }
}

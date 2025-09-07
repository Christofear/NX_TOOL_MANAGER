using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NX_TOOL_MANAGER.Converters
{
    /// <summary>
    /// Converts a boolean to a GridLength, allowing for a parameter to define the expanded size.
    /// </summary>
    public class BooleanToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded && isExpanded)
            {
                // EXPANDED STATE: Use the parameter to define the width.
                string size = parameter as string ?? "Auto";
                if (size.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                {
                    return new GridLength(1, GridUnitType.Auto);
                }
                if (double.TryParse(size, out double pixels))
                {
                    return new GridLength(pixels);
                }
                // Fallback for star (*) sizing
                return new GridLength(1, GridUnitType.Star);
            }

            // COLLAPSED STATE: The column width becomes "Auto" to shrink to its content (the vertical bar).
            return GridLength.Auto;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


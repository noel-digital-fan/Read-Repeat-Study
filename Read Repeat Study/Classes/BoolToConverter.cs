using System.Globalization;

namespace Read_Repeat_Study.Classes
{
    public class BoolToColorConverter : IValueConverter // Converts a boolean value to a Color
    {
        public Color TrueColor { get; set; } // Color when the boolean value is true
        public Color FalseColor { get; set; } // Color when the boolean value is false

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) // Convert bool to Color
        {
            return (value is bool b && b) ? TrueColor : FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) // Convert Color back to bool (not implemented)
        {
            throw new NotImplementedException();
        }
    }

    public class InvertedBoolConverter : IValueConverter // Inverts a boolean value
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) // Invert bool value
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) // Invert bool value back
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    public class BoolToOpacityConverter : IValueConverter // Converts a boolean value to an opacity value
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) // Convert bool to opacity (1.0 for true, 0.4 for false)
        {
            if (value is bool boolValue)
            {
                return boolValue ? 1.0 : 0.4;
            }
            return 0.4;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) // Convert opacity back to bool (not implemented)
        {
            throw new NotImplementedException();
        }
    }

    public class ColorFromNameConverter : IValueConverter // Converts a color name string to a Color object
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) // Convert color name string to Color object
        {
            if (value is string colorString && !string.IsNullOrWhiteSpace(colorString)) // Check if the string is not null or empty
            {
                try
                {
                    return Color.FromArgb(colorString);
                }
                catch
                {
                    return Colors.Transparent;
                }
            }
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) // Convert Color object back to color name string (not implemented)
        {
            throw new NotImplementedException();
        }
    }
}

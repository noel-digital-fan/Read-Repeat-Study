using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Read_Repeat_Study.Classes
{
    public class MathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) // Converts an integer value by adding 1 (e.g., for page numbers)
        {
            if (value is int intValue)
            {
                return intValue + 1;
            }
            
            if (value is null)
            {
                return 1; // Default to page 1 if null
            }
            
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) // Convert back is not implemented
        {
            throw new NotImplementedException();
        }
    }
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace FinanceManager.Converters
{
    public class GreaterThanZeroConverter : IValueConverter
    {
        public static GreaterThanZeroConverter Instance { get; } = new GreaterThanZeroConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return decimalValue > 0;

            if (value is double doubleValue)
                return doubleValue > 0;

            if (value is float floatValue)
                return floatValue > 0;

            if (value is int intValue)
                return intValue > 0;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LessThanZeroConverter : IValueConverter
    {
        public static LessThanZeroConverter Instance { get; } = new LessThanZeroConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return decimalValue < 0;

            if (value is double doubleValue)
                return doubleValue < 0;

            if (value is float floatValue)
                return floatValue < 0;

            if (value is int intValue)
                return intValue < 0;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProfitLossColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                if (decimalValue > 0)
                    return System.Windows.Media.Brushes.Green;
                else if (decimalValue < 0)
                    return System.Windows.Media.Brushes.Red;
                else
                    return System.Windows.Media.Brushes.Black;
            }

            return System.Windows.Media.Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

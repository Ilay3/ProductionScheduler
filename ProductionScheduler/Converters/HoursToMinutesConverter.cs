using System;
using System.Globalization;
using System.Windows.Data;

namespace ProductionScheduler.Converters
{
    public class HoursToMinutesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double hours)
            {
                return hours * 60;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double minutes)
            {
                return minutes / 60;
            }
            return 0;
        }
    }
}
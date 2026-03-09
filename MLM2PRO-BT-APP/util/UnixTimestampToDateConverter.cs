using System.Globalization;
using System.Windows.Data;

namespace MLM2PRO_BT_APP.util
{
    public class UnixTimestampToDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long unixTimestamp && unixTimestamp > 0)
            {
                try
                {
                    var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
                    var localDateTime = dateTimeOffset.ToLocalTime();
                    
                    // Format: "Monday March 9 2026, 2:50 PM"
                    return localDateTime.ToString("dddd MMMM d yyyy, h:mm tt", CultureInfo.CurrentCulture);
                }
                catch
                {
                    return "Invalid date";
                }
            }
            
            return "No expiration date";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

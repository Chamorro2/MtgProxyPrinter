using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MtgProxyPrinterEs.Converters
{
    /// <summary>
    /// Converts a bool value to Visibility.
    /// true  → Visible
    /// false → Collapsed
    /// Used in XAML to show/hide elements based on ViewModel properties.
    /// Example: Visibility="{Binding ShowProgress, Converter={StaticResource BoolToVis}}"
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts bool → Visibility.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return b ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed; // If value is not bool, hide by default for safety
        }

        /// <summary>
        /// Converts Visibility → bool (for two-way bindings, rarely used).
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v) return v == Visibility.Visible;
            return false;
        }
    }

    /// <summary>
    /// Inverts a bool value.
    /// true  → false
    /// false → true
    /// Used to disable controls while the app is busy loading or processing.
    /// Example: IsEnabled="{Binding IsBusy, Converter={StaticResource InvBool}}"
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        /// <summary>
        /// Inverts the received bool. Returns true by default if value is not a bool.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true; // If not a bool, assume not busy
        }

        /// <summary>
        /// Inverts back (for two-way bindings).
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }
}
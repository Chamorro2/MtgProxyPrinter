using System.Windows;

namespace MtgProxyPrinterEs.Services
{
    /// <summary>
    /// Singleton service that manages the application's UI language at runtime.
    /// Works by swapping WPF ResourceDictionary files containing localized strings.
    /// Supported languages: "es" (Spanish), "en" (English).
    /// </summary>
    public class LocalizationService
    {
        // Singleton instance — only one localization service exists per app session
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        /// <summary>Currently active language code (e.g. "es", "en").</summary>
        private string _currentLang = "es";
        public string CurrentLang => _currentLang;

        /// <summary>
        /// Switches the application language by replacing the active string ResourceDictionary.
        /// WPF DynamicResource bindings update automatically when the dictionary is swapped,
        /// so all bound UI elements refresh instantly without restarting the app.
        /// </summary>
        /// <param name="lang">Language code to switch to (e.g. "es", "en").</param>
        public void SetLanguage(string lang)
        {
            _currentLang = lang;

            // Build the new dictionary from the corresponding Strings.{lang}.xaml file
            var dict = new ResourceDictionary
            {
                Source = new Uri($"Resources/Strings.{lang}.xaml", UriKind.Relative)
            };

            // Find and remove the currently loaded string dictionary (if any)
            var current = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));

            if (current != null)
                Application.Current.Resources.MergedDictionaries.Remove(current);

            // Add the new dictionary — DynamicResource bindings update automatically
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        /// <summary>
        /// Retrieves a localized string by its resource key from the current dictionary.
        /// If the key is not found, returns the key itself as a fallback.
        /// Useful for accessing strings from code-behind instead of XAML.
        /// </summary>
        /// <param name="key">The resource key defined in Strings.{lang}.xaml.</param>
        public string Get(string key)
        {
            return Application.Current.Resources[key] as string ?? key;
        }
    }
}
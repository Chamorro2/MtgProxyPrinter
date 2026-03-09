using MtgProxyPrinterEs.Models;
using MtgProxyPrinterEs.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MtgProxyPrinterEs
{
    /// <summary>
    /// Main application window.
    /// Responsible for displaying the deck list, handling user interaction,
    /// and showing a preview of the selected card artwork.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Main ViewModel that manages the application state,
        /// including the deck entries and commands.
        /// </summary>
        private readonly MainViewModel _vm;

        /// <summary>
        /// Initializes the main window and assigns the ViewModel
        /// as the DataContext for data binding.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
        }

        /// <summary>
        /// Opens the artwork selection dialog for a specific deck entry.
        /// The button's Tag property contains the DeckEntry associated with the row.
        /// After closing the dialog, the entry is refreshed and the preview updated.
        /// </summary>
        private async void ArtButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not DeckEntry entry) return;

            var dialog = new ArtSelectorWindow(_vm._scryfall, entry);
            dialog.Owner = this;
            dialog.ShowDialog();

            _vm.RefreshEntry(entry);
            ShowPreview(entry);
        }

        /// <summary>
        /// Triggered when the selected item in the deck list changes.
        /// Updates the card preview if a valid entry is selected.
        /// </summary>
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm.SelectedEntry != null)
                ShowPreview(_vm.SelectedEntry);
        }

        /// <summary>
        /// Displays the selected card artwork in the preview image area.
        /// Retrieves the image URL from the selected print and loads it into a BitmapImage.
        /// </summary>
        private void ShowPreview(DeckEntry entry)
        {
            if (entry.SelectedPrint == null) return;

            var url = entry.SelectedPrint.GetImageUrl("normal");
            if (url == null) return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            PreviewImage.Source = bitmap;
        }
    }
}
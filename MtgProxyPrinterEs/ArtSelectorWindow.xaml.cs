using MtgProxyPrinterEs.Models;
using MtgProxyPrinterEs.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MtgProxyPrinterEs
{
    /// <summary>
    /// Window that allows the user to select a specific artwork (printing)
    /// of a Magic: The Gathering card retrieved from the Scryfall API.
    /// Displays all available prints and allows filtering by language.
    /// </summary>
    public partial class ArtSelectorWindow : Window
    {
        /// <summary>
        /// Service responsible for communicating with the Scryfall API.
        /// Used to fetch card prints and download images.
        /// </summary>
        private readonly ScryfallService _scryfall;

        /// <summary>
        /// Deck entry representing the selected card in the deck list.
        /// The chosen print will be stored here.
        /// </summary>
        private readonly DeckEntry _entry;

        /// <summary>
        /// Cached list of all printings available for the card.
        /// </summary>
        private List<ScryfallCard> _allPrints = new();

        /// <summary>
        /// Constructor for the art selector window.
        /// Receives the Scryfall service and the deck entry to modify.
        /// </summary>
        public ArtSelectorWindow(ScryfallService scryfall, DeckEntry entry)
        {
            InitializeComponent();

            _scryfall = scryfall;
            _entry = entry;

            // Display the card name in the UI
            CardNameRun.Text = entry.CardName;
        }

        /// <summary>
        /// Triggered when the window content has finished rendering.
        /// Used to start loading the available card prints.
        /// </summary>
        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            await LoadEditionsAsync();
        }

        /// <summary>
        /// Loads all available card printings.
        /// If they are already cached in the deck entry they are reused,
        /// otherwise they are requested from the Scryfall API.
        /// </summary>
        private async Task LoadEditionsAsync()
        {
            LoadingText.Visibility = Visibility.Visible;
            PrintsList.Visibility = Visibility.Collapsed;
            StatusText.Text = "Searching all editions...";

            if (_entry.AvailablePrints.Any())
                _allPrints = _entry.AvailablePrints;
            else
                _allPrints = await _scryfall.GetAllPrintsAsync(_entry.CardName);

            ShowEditions();
        }

        /// <summary>
        /// Prepares the language filter based on the available prints
        /// and then displays the filtered results.
        /// </summary>
        private void ShowEditions()
        {
            var languages = _allPrints
                .Select(p => p.Lang)
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            languages.Insert(0, "All");

            var currentSelection = LangFilter.SelectedItem as string;

            LangFilter.SelectionChanged -= LangFilter_SelectionChanged;

            LangFilter.ItemsSource = languages;

            LangFilter.SelectedItem = currentSelection != null && languages.Contains(currentSelection)
                ? currentSelection
                : "All";

            LangFilter.SelectionChanged += LangFilter_SelectionChanged;

            FilterAndShow();
        }

        /// <summary>
        /// Filters the available prints by language and updates the list UI.
        /// Also triggers asynchronous image loading.
        /// </summary>
        private void FilterAndShow()
        {
            var selected = LangFilter.SelectedItem as string;

            var filtered = selected == null || selected == "All"
                ? _allPrints
                : _allPrints.Where(p => p.Lang == selected).ToList();

            PrintsList.Items.Clear();

            foreach (var card in filtered)
                PrintsList.Items.Add(card);

            CountText.Text = $"{filtered.Count} editions found";
            StatusText.Text = "";
            LoadingText.Visibility = Visibility.Collapsed;
            PrintsList.Visibility = Visibility.Visible;

            _ = LoadImagesAsync(filtered);

            if (_entry.SelectedPrint != null)
            {
                var actual = filtered.FirstOrDefault(p => p.Id == _entry.SelectedPrint.Id);
                if (actual != null)
                    PrintsList.SelectedItem = actual;
            }
        }

        /// <summary>
        /// Asynchronously downloads and assigns images for each card printing.
        /// Images are downloaded from Scryfall and injected into the UI.
        /// </summary>
        private async Task LoadImagesAsync(List<ScryfallCard> cards)
        {
            foreach (var card in cards)
            {
                var url = card.GetImageUrl("normal");
                if (url == null) continue;

                try
                {
                    var bytes = await _scryfall.DownloadImageAsync(url);
                    if (bytes == null) continue;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        var item = PrintsList.ItemContainerGenerator.ContainerFromItem(card) as ListBoxItem;
                        if (item == null) return;

                        var image = FindVisualChild<Image>(item);
                        if (image == null) return;

                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new System.IO.MemoryStream(bytes);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        image.Source = bitmap;
                    });
                }
                catch { }
            }
        }

        /// <summary>
        /// Recursively searches for a visual child element of a specific type
        /// within the WPF visual tree.
        /// </summary>
        private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is T t) return t;

                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// Triggered when the selected print changes.
        /// Updates the preview panel with card information and image.
        /// </summary>
        private void PrintsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PrintsList.SelectedItem is not ScryfallCard card) return;

            ConfirmButton.IsEnabled = true;
            SelectedName.Text = card.DisplayName;
            SelectedInfo.Text = card.DisplayInfo;

            var url = card.GetImageUrl("normal");
            if (url == null) return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            PreviewImage.Source = bitmap;
        }

        /// <summary>
        /// Triggered when the language filter changes.
        /// Reapplies the filter to the available prints.
        /// </summary>
        private void LangFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allPrints.Any())
                FilterAndShow();
        }

        /// <summary>
        /// Confirms the selected printing and stores it in the deck entry.
        /// Closes the window returning DialogResult = true.
        /// </summary>
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrintsList.SelectedItem is ScryfallCard card)
                _entry.SelectedPrint = card;

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Cancels the selection and closes the window.
        /// DialogResult = false indicates no changes were made.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MtgProxyPrinterEs.Models;
using MtgProxyPrinterEs.Services;
using System.Collections.ObjectModel;

namespace MtgProxyPrinterEs.ViewModels
{
    /// <summary>
    /// Main ViewModel for the application. Follows the MVVM pattern using CommunityToolkit.Mvvm.
    /// Manages the deck list, card loading, PDF generation, and UI language switching.
    /// Bound to MainWindow.xaml via DataContext.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        /// <summary>
        /// Scryfall service instance. Public so MainWindow.xaml.cs can pass it
        /// to ArtSelectorWindow when the user opens the art picker.
        /// </summary>
        public readonly ScryfallService _scryfall = new();

        /// <summary>PDF generation service. Uses the same Scryfall instance for image downloads.</summary>
        private readonly PdfGeneratorService _pdfGenerator;

        /// <summary>
        /// Initializes services and sets the default UI language.
        /// </summary>
        public MainViewModel()
        {
            _pdfGenerator = new PdfGeneratorService(_scryfall);

            // Apply default language on startup so DynamicResource bindings resolve correctly
            LocalizationService.Instance.SetLanguage(_idiomaApp);
        }

        // ─── Observable Properties (auto-generates getters, setters, and PropertyChanged) ───

        /// <summary>Raw text from the deck list TextBox.</summary>
        [ObservableProperty] private string _deckListText = "";

        /// <summary>Message shown in the status bar at the bottom of the window.</summary>
        [ObservableProperty] private string _statusMessage = "Ready. Paste your deck list and press 'Load Cards'.";

        /// <summary>Current value of the progress bar.</summary>
        [ObservableProperty] private int _progressValue = 0;

        /// <summary>Maximum value of the progress bar (total cards to process).</summary>
        [ObservableProperty] private int _progressMax = 100;

        /// <summary>True while cards are being loaded or PDF is being generated. Disables UI controls.</summary>
        [ObservableProperty] private bool _isBusy = false;

        /// <summary>Controls visibility of the progress bar in the status bar.</summary>
        [ObservableProperty] private bool _showProgress = false;

        /// <summary>Currently selected entry in the deck list. Drives the preview panel.</summary>
        [ObservableProperty] private DeckEntry? _selectedEntry;

        /// <summary>
        /// Preferred card language for Scryfall searches (e.g. "es", "en", "ja").
        /// Used when searching cards — tries this language first, then falls back to English.
        /// </summary>
        [ObservableProperty] private string _idiomaPreferido = "es";

        // ─── UI Language (manual property to trigger LocalizationService on change) ───

        /// <summary>Backing field for the UI language selector.</summary>
        private string _idiomaApp = "es";

        /// <summary>
        /// Currently selected UI language ("es" or "en").
        /// When changed, immediately swaps the ResourceDictionary via LocalizationService,
        /// causing all DynamicResource bindings in the UI to update instantly.
        /// </summary>
        public string IdiomaApp
        {
            get => _idiomaApp;
            set
            {
                if (_idiomaApp == value) return;
                _idiomaApp = value;
                OnPropertyChanged();
                LocalizationService.Instance.SetLanguage(value);
            }
        }

        // ─── Static Lists ───

        /// <summary>
        /// All card languages available in the preferred language selector.
        /// These are the language codes supported by the Scryfall API.
        /// </summary>
        public List<string> IdiomasDisponibles { get; } = new()
        {
            "es", "en", "fr", "de", "it", "pt", "ja", "ko", "ru", "zhs", "zht"
        };

        /// <summary>UI languages available for the app interface (Spanish and English).</summary>
        public List<string> IdiomasApp { get; } = new() { "es", "en" };

        /// <summary>
        /// The deck entries currently loaded in the UI.
        /// ObservableCollection ensures the ListBox updates automatically when items are added or removed.
        /// </summary>
        public ObservableCollection<DeckEntry> DeckEntries { get; } = new();

        // ─── Commands ───

        /// <summary>
        /// Parses the deck list text and searches Scryfall for each card.
        /// Runs searches sequentially to respect the API rate limit.
        /// Also triggers a background load of all available printings per card.
        /// </summary>
        [RelayCommand]
        private async Task LoadCardsAsync()
        {
            if (string.IsNullOrWhiteSpace(DeckListText))
            {
                StatusMessage = "The deck list is empty.";
                return;
            }

            IsBusy = true;
            ShowProgress = true;
            DeckEntries.Clear();

            // Parse each line of the deck list into DeckEntry objects
            var lines = DeckListText.Split('\n');
            var entries = lines
                .Select(ScryfallService.ParseDeckLine)
                .Where(e => e != null)
                .Cast<DeckEntry>()
                .ToList();

            if (entries.Count == 0)
            {
                StatusMessage = "Could not parse any lines from the deck list.";
                IsBusy = false;
                ShowProgress = false;
                return;
            }

            ProgressMax = entries.Count;
            ProgressValue = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.IsLoading = true;
                DeckEntries.Add(entry);
                StatusMessage = $"Searching '{entry.CardName}'... ({i + 1}/{entries.Count})";

                // Search Scryfall using the preferred language with English fallback
                var card = await _scryfall.SearchCardAsync(
                    entry.CardName, entry.SetCode, entry.CollectorNumber, IdiomaPreferido);

                if (card != null)
                {
                    entry.SelectedPrint = card;

                    // Load all printings in the background for the art selector
                    _ = LoadAllPrintsAsync(entry);
                }
                else
                {
                    entry.HasError = true;
                    entry.ErrorMessage = "Not found";
                }

                entry.IsLoading = false;
                RefreshEntry(entry); // Force ListBox to re-render the updated entry
                ProgressValue = i + 1;
            }

            var loaded = DeckEntries.Count(e => e.IsLoaded);
            var errors = DeckEntries.Count(e => e.HasError);
            StatusMessage = $"{loaded} cards loaded, {errors} errors.";
            IsBusy = false;
            ShowProgress = false;
        }

        /// <summary>
        /// Loads all available printings for a card entry in the background.
        /// Results are stored in entry.AvailablePrints for use by ArtSelectorWindow.
        /// Fire-and-forget: called with _ = to avoid blocking the main load loop.
        /// </summary>
        private async Task LoadAllPrintsAsync(DeckEntry entry)
        {
            var prints = await _scryfall.GetAllPrintsAsync(entry.CardName);
            entry.AvailablePrints = prints;
        }

        /// <summary>
        /// Forces the ListBox to re-render a specific entry by removing and re-inserting it.
        /// Necessary because DeckEntry does not implement INotifyPropertyChanged —
        /// the ObservableCollection only detects add/remove, not property changes on items.
        /// </summary>
        public void RefreshEntry(DeckEntry entry)
        {
            var idx = DeckEntries.IndexOf(entry);
            if (idx < 0) return;
            DeckEntries.RemoveAt(idx);
            DeckEntries.Insert(idx, entry);
        }

        /// <summary>
        /// Loads a sample deck into the text box for quick testing.
        /// Uses string concatenation instead of a verbatim string to avoid
        /// accidental leading spaces from code indentation.
        /// </summary>
        [RelayCommand]
        private void LoadSampleDeck()
        {
            DeckListText = "1 Wyll, Blade of Frontiers (CLB) 208\n" +
                           "1 Ancient Copper Dragon (CLB) 161\n" +
                           "1 Goldspan Dragon (M3C) 212\n" +
                           "1 Sol Ring (ECC) 57\n" +
                           "1 Command Tower (ECC) 59\n" +
                           "1 Counterspell (DSC) 114\n" +
                           "9 Island (ECL) 270\n" +
                           "7 Mountain (ECL) 272";
        }

        /// <summary>
        /// Generates a PDF proxy sheet from all successfully loaded cards.
        /// Opens a SaveFileDialog to let the user choose the output path.
        /// After generation, offers to open the file automatically.
        /// </summary>
        [RelayCommand]
        private async Task GeneratePdfAsync()
        {
            var readyEntries = DeckEntries.Where(e => e.IsLoaded).ToList();
            if (!readyEntries.Any())
            {
                StatusMessage = "No cards loaded.";
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "Deck_Proxy.pdf",
                DefaultExt = ".pdf",
                Filter = "PDF Files (*.pdf)|*.pdf"
            };

            if (dialog.ShowDialog() != true) return;

            IsBusy = true;
            ShowProgress = true;

            int totalCards = readyEntries.Sum(e => e.Quantity);
            ProgressMax = totalCards;
            ProgressValue = 0;

            // Progress callback: updates the progress bar and status message per card
            var progress = new Progress<(int current, int total, string message)>(p =>
            {
                ProgressValue = p.current;
                StatusMessage = p.message;
            });

            try
            {
                await _pdfGenerator.GeneratePdfAsync(readyEntries, dialog.FileName, progress);
                StatusMessage = $"PDF generated: {dialog.FileName}";

                // Offer to open the generated PDF immediately
                var result = System.Windows.MessageBox.Show(
                    "PDF generated successfully. Would you like to open it?",
                    "Done", System.Windows.MessageBoxButton.YesNo);

                if (result == System.Windows.MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }

            IsBusy = false;
            ShowProgress = false;
        }
    }
}
using MtgProxyPrinterEs.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.IO;

namespace MtgProxyPrinterEs.Services
{
    /// <summary>
    /// Service responsible for generating a print-ready PDF from the deck list.
    /// Layout: A4 page (21x29.7cm), 3 columns x 3 rows, 9 cards per page.
    /// Each card is sized to standard MTG card dimensions (6.35 x 8.89 cm).
    /// Images are cached locally to avoid re-downloading on every PDF generation.
    /// </summary>
    public class PdfGeneratorService
    {
        private readonly ScryfallService _scryfall;

        /// <summary>Local folder where downloaded card images are cached.</summary>
        private readonly string _cacheFolder;

        /// <summary>
        /// Creates a new PdfGeneratorService.
        /// The cache folder is created automatically if it does not exist.
        /// </summary>
        /// <param name="scryfall">Scryfall service used to download card images.</param>
        /// <param name="cacheFolder">Folder path for the local image cache. Defaults to "cache_cartas".</param>
        public PdfGeneratorService(ScryfallService scryfall, string cacheFolder = "cache_cartas")
        {
            _scryfall = scryfall;
            _cacheFolder = cacheFolder;
            Directory.CreateDirectory(_cacheFolder);
        }

        /// <summary>
        /// Generates a PDF file from the provided deck entries and saves it to outputPath.
        /// Cards are placed in order: left to right, top to bottom, 9 per page.
        /// Multiple copies of the same card are placed on consecutive slots.
        /// Reports progress after each card is processed.
        /// </summary>
        /// <param name="deck">List of deck entries with selected printings.</param>
        /// <param name="outputPath">Full file path where the PDF will be saved.</param>
        /// <param name="progress">Optional progress reporter: (current, total, message).</param>
        public async Task GeneratePdfAsync(
            List<DeckEntry> deck,
            string outputPath,
            IProgress<(int current, int total, string message)>? progress = null)
        {
            var document = new PdfDocument();

            // Standard MTG card size in points (1cm = 28.35pt)
            double cardW = XUnit.FromCentimeter(6.35).Point;
            double cardH = XUnit.FromCentimeter(8.89).Point;

            // A4 page dimensions in points
            double pageW = XUnit.FromCentimeter(21.0).Point;
            double pageH = XUnit.FromCentimeter(29.7).Point;

            // Center the 3x3 grid on the page
            double marginX = (pageW - 3 * cardW) / 2.0;
            double marginY = (pageH - 3 * cardH) / 2.0;

            PdfPage? currentPage = null;
            XGraphics? gfx = null;

            int cardIndex = 0;
            int totalCards = deck.Sum(e => e.Quantity);
            int processed = 0;

            foreach (var entry in deck)
            {
                if (entry.SelectedPrint == null) continue;

                progress?.Report((processed, totalCards, $"Downloading {entry.CardName}..."));

                // Get image from cache or download from Scryfall
                var imageBytes = await GetCardImageAsync(entry.SelectedPrint);
                if (imageBytes == null) continue;

                // Place one slot per copy of the card
                for (int i = 0; i < entry.Quantity; i++)
                {
                    int posInPage = cardIndex % 9;

                    // Start a new page every 9 cards
                    if (posInPage == 0)
                    {
                        gfx?.Dispose();
                        currentPage = document.AddPage();
                        currentPage.Width = pageW;
                        currentPage.Height = pageH;
                        gfx = XGraphics.FromPdfPage(currentPage);
                    }

                    // Calculate grid position (col 0-2, row 0-2)
                    int col = posInPage % 3;
                    int row = posInPage / 3;

                    double x = marginX + col * cardW;
                    double y = marginY + row * cardH;

                    // Draw the card image
                    using var ms = new MemoryStream(imageBytes);
                    var xImage = XImage.FromStream(ms);
                    gfx!.DrawImage(xImage, x, y, cardW, cardH);

                    // Draw light gray cut lines around each card
                    var pen = new XPen(XColors.LightGray, 0.5);
                    gfx.DrawRectangle(pen, x, y, cardW, cardH);

                    cardIndex++;
                    processed++;
                    progress?.Report((processed, totalCards, $"{entry.CardName} ({processed}/{totalCards})"));
                }
            }

            gfx?.Dispose();
            document.Save(outputPath);
        }

        /// <summary>
        /// Returns the image bytes for a card, using the local cache when available.
        /// Cache file name format: {Name}_{Set}_{CollectorNumber}_{Lang}.jpg
        /// If not cached, downloads from Scryfall and saves to the cache folder.
        /// </summary>
        private async Task<byte[]?> GetCardImageAsync(ScryfallCard card)
        {
            var safeName = MakeSafeFileName($"{card.Name}_{card.Set}_{card.CollectorNumber}_{card.Lang}");
            var cachePath = Path.Combine(_cacheFolder, safeName + ".jpg");

            // Return cached image if it exists
            if (File.Exists(cachePath))
                return await File.ReadAllBytesAsync(cachePath);

            // Download from Scryfall and cache it
            var url = card.GetImageUrl("large");
            if (url == null) return null;

            var bytes = await _scryfall.DownloadImageAsync(url);
            if (bytes != null)
                await File.WriteAllBytesAsync(cachePath, bytes);

            return bytes;
        }

        /// <summary>
        /// Sanitizes a string to be safe for use as a file name.
        /// Replaces invalid characters and spaces with underscores.
        /// </summary>
        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
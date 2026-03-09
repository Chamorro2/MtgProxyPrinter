using MtgProxyPrinterEs.Models;
using Newtonsoft.Json;
using System.Net.Http;

namespace MtgProxyPrinterEs.Services
{
    /// <summary>
    /// Service that handles all communication with the Scryfall API.
    /// Responsibilities: card search, image downloading, deck line parsing, and rate limiting.
    /// API docs: https://scryfall.com/docs/api
    /// </summary>
    public class ScryfallService
    {
        /// <summary>
        /// Shared HttpClient instance. Static to avoid socket exhaustion.
        /// Initialized via a factory method because headers cannot be added
        /// using collection initializer syntax on DefaultRequestHeaders.
        /// </summary>
        private static readonly HttpClient _http = CreateHttpClient();

        /// <summary>
        /// Creates and configures the HttpClient with the required headers.
        /// Scryfall requires a User-Agent header and recommends Accept: application/json.
        /// </summary>
        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri("https://api.scryfall.com/")
            };
            client.DefaultRequestHeaders.Add("User-Agent", "MtgProxyPrinter/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            return client;
        }

        /// <summary>
        /// Semaphore used to enforce a single concurrent request at a time.
        /// Combined with a minimum delay between requests to respect Scryfall's rate limit.
        /// </summary>
        private readonly SemaphoreSlim _rateLimiter = new(1, 1);
        private DateTime _lastRequest = DateTime.MinValue;
        private const int DelayMs = 100; // Scryfall recommends max 10 requests/second

        /// <summary>
        /// Ensures a minimum delay of 100ms between API requests.
        /// Uses a semaphore to prevent concurrent requests from bypassing the delay.
        /// </summary>
        private async Task RateLimit()
        {
            await _rateLimiter.WaitAsync();
            try
            {
                var elapsed = (DateTime.Now - _lastRequest).TotalMilliseconds;
                if (elapsed < DelayMs)
                    await Task.Delay((int)(DelayMs - elapsed));
                _lastRequest = DateTime.Now;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        /// <summary>
        /// Searches for a card using a three-step fallback strategy:
        /// 1. Search by name in the preferred language.
        /// 2. If not found, fall back to English.
        /// 3. If still not found and set+number are provided, fetch the exact printing.
        /// </summary>
        /// <param name="name">Card name to search for.</param>
        /// <param name="setCode">Optional set code (e.g. "M21").</param>
        /// <param name="collectorNumber">Optional collector number (e.g. "152").</param>
        /// <param name="preferredLanguage">Preferred language code (e.g. "es", "en").</param>
        public async Task<ScryfallCard?> SearchCardAsync(
            string name,
            string? setCode = null,
            string? collectorNumber = null,
            string preferredLanguage = "en")
        {
            // Step 1: Try preferred language
            var preferred = await SearchByNameAndLangAsync(name, preferredLanguage);
            if (preferred != null) return preferred;

            // Step 2: Fall back to English if preferred language failed
            if (preferredLanguage != "en")
            {
                var en = await SearchByNameAndLangAsync(name, "en");
                if (en != null) return en;
            }

            // Step 3: Last resort — fetch exact printing by set + collector number
            if (!string.IsNullOrEmpty(setCode) && !string.IsNullOrEmpty(collectorNumber))
            {
                var exact = await GetCardBySetAndNumberAsync(setCode, collectorNumber);
                if (exact != null) return exact;
            }

            return null;
        }

        /// <summary>
        /// Searches Scryfall for an exact card name in a specific language.
        /// Uses the "!" prefix for exact name matching to avoid partial matches.
        /// Returns the first result or null if not found.
        /// </summary>
        private async Task<ScryfallCard?> SearchByNameAndLangAsync(string name, string lang)
        {
            await RateLimit();
            try
            {
                var query = Uri.EscapeDataString($"!\"{name}\" lang:{lang}");
                var response = await _http.GetAsync($"cards/search?q={query}");
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ScryfallSearchResult>(json);
                return result?.Data?.FirstOrDefault();
            }
            catch { return null; }
        }

        /// <summary>
        /// Fetches a specific card printing directly by set code and collector number.
        /// Used as a last resort when name search fails but set info is available.
        /// </summary>
        private async Task<ScryfallCard?> GetCardBySetAndNumberAsync(string set, string number)
        {
            await RateLimit();
            try
            {
                var response = await _http.GetAsync($"cards/{set.ToLower()}/{number}");
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ScryfallCard>(json);
            }
            catch { return null; }
        }

        /// <summary>
        /// Fetches all available printings of a card across all sets and languages.
        /// Strategy:
        /// 1. Search for the English version of the card to get its prints_search_uri.
        /// 2. Use that URI with &include_multilingual=true to get all language variants.
        /// Only returns printings that have a valid image URL.
        /// </summary>
        /// <param name="cardName">English card name to look up.</param>
        public async Task<List<ScryfallCard>> GetAllPrintsAsync(string cardName)
        {
            await RateLimit();
            var allPrints = new List<ScryfallCard>();
            try
            {
                // Step 1: Get the base English card to retrieve prints_search_uri
                var query = Uri.EscapeDataString($"!\"{cardName}\" lang:en");
                var response = await _http.GetAsync($"cards/search?q={query}");
                if (!response.IsSuccessStatusCode) return allPrints;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ScryfallSearchResult>(json);
                var baseCard = result?.Data?.FirstOrDefault();

                if (baseCard?.PrintsSearchUri == null) return allPrints;

                // Step 2: Fetch all printings including multilingual versions
                await RateLimit();
                var printsResponse = await _http.GetAsync(baseCard.PrintsSearchUri + "&include_multilingual=true");
                if (!printsResponse.IsSuccessStatusCode) return allPrints;

                var printsJson = await printsResponse.Content.ReadAsStringAsync();
                var printsResult = JsonConvert.DeserializeObject<ScryfallSearchResult>(printsJson);

                // Only keep printings that have a usable image
                if (printsResult?.Data != null)
                    allPrints.AddRange(printsResult.Data.Where(c => c.GetImageUrl() != null));
            }
            catch { }

            return allPrints;
        }

        /// <summary>
        /// Downloads an image from a URL and returns the raw bytes.
        /// Rate limited to respect Scryfall's image CDN.
        /// Returns null if the download fails for any reason.
        /// </summary>
        public async Task<byte[]?> DownloadImageAsync(string url)
        {
            try
            {
                await RateLimit();
                return await _http.GetByteArrayAsync(url);
            }
            catch { return null; }
        }

        /// <summary>
        /// Parses a single line from a deck list into a DeckEntry.
        /// Supports two formats:
        ///   With quantity:    "4 Lightning Bolt (M21) 152"  or  "4 Lightning Bolt"
        ///   Without quantity: "Lightning Bolt (M21) 152"    or  "Lightning Bolt"
        /// Lines starting with "//" or "#" are treated as comments and ignored.
        /// Returns null for empty lines, comments, or lines that cannot be parsed.
        /// </summary>
        public static DeckEntry? ParseDeckLine(string line)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("#"))
                return null;

            // Pattern 1: line starts with a number (quantity included)
            // Captures: (quantity) (card name) optional[(set) (collector number)]
            var matchWithQuantity = System.Text.RegularExpressions.Regex.Match(
                line,
                @"^(\d+)\s+(.+?)(?:\s+\(([A-Za-z0-9]+)\)\s+(\S+))?\s*$"
            );

            if (matchWithQuantity.Success)
                return new DeckEntry
                {
                    Quantity = int.Parse(matchWithQuantity.Groups[1].Value),
                    CardName = matchWithQuantity.Groups[2].Value.Trim(),
                    SetCode = matchWithQuantity.Groups[3].Success ? matchWithQuantity.Groups[3].Value : null,
                    CollectorNumber = matchWithQuantity.Groups[4].Success ? matchWithQuantity.Groups[4].Value : null
                };

            // Pattern 2: no quantity prefix, assume 1 copy
            // Captures: (card name) optional[(set) (collector number)]
            var matchWithoutQuantity = System.Text.RegularExpressions.Regex.Match(
                line,
                @"^(.+?)(?:\s+\(([A-Za-z0-9]+)\)\s+(\S+))?\s*$"
            );

            if (matchWithoutQuantity.Success)
                return new DeckEntry
                {
                    Quantity = 1,
                    CardName = matchWithoutQuantity.Groups[1].Value.Trim(),
                    SetCode = matchWithoutQuantity.Groups[2].Success ? matchWithoutQuantity.Groups[2].Value : null,
                    CollectorNumber = matchWithoutQuantity.Groups[3].Success ? matchWithoutQuantity.Groups[3].Value : null
                };

            return null;
        }
    }
}
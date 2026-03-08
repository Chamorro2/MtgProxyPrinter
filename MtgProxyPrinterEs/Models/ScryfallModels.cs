using Newtonsoft.Json;

namespace MtgProxyPrinterEs.Models
{
    /// <summary>
    /// Represents the paginated response from the Scryfall search API.
    /// The "data" field contains the list of matching cards.
    /// </summary>
    public class ScryfallSearchResult
    {
        [JsonProperty("data")]
        public List<ScryfallCard> Data { get; set; } = new();
    }

    /// <summary>
    /// Represents a single card printing returned by the Scryfall API.
    /// Each printing is unique by set, collector number, and language.
    /// </summary>
    public class ScryfallCard
    {
        /// <summary>Unique Scryfall ID for this specific printing.</summary>
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>English card name, always present.</summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>Translated card name, only present for non-English printings.</summary>
        [JsonProperty("printed_name")]
        public string? PrintedName { get; set; }

        /// <summary>Three-letter set code (e.g. "m21", "clb").</summary>
        [JsonProperty("set")]
        public string Set { get; set; } = "";

        /// <summary>Full set name (e.g. "Core Set 2021").</summary>
        [JsonProperty("set_name")]
        public string SetName { get; set; } = "";

        /// <summary>Collector number within the set (e.g. "152").</summary>
        [JsonProperty("collector_number")]
        public string CollectorNumber { get; set; } = "";

        /// <summary>Language code for this printing (e.g. "en", "es", "ja").</summary>
        [JsonProperty("lang")]
        public string Lang { get; set; } = "";

        /// <summary>Image URLs for standard single-faced cards.</summary>
        [JsonProperty("image_uris")]
        public ImageUris? ImageUris { get; set; }

        /// <summary>
        /// Card faces for double-faced cards (DFCs).
        /// Each face has its own image and name.
        /// </summary>
        [JsonProperty("card_faces")]
        public List<CardFace>? CardFaces { get; set; }

        /// <summary>
        /// URL to search for all printings of this card across sets.
        /// Returned by Scryfall and used to populate the art selector.
        /// </summary>
        [JsonProperty("prints_search_uri")]
        public string? PrintsSearchUri { get; set; }

        /// <summary>
        /// Display name: uses the translated name if available, otherwise falls back to English.
        /// </summary>
        public string DisplayName => PrintedName ?? Name;

        /// <summary>
        /// Human-readable edition info shown in the UI.
        /// Format: "Set Name (SET) #123 [LANG]"
        /// </summary>
        public string DisplayInfo => $"{SetName} ({Set.ToUpper()}) #{CollectorNumber} [{Lang.ToUpper()}]";

        /// <summary>
        /// Returns the image URL for the requested size ("normal" or "large").
        /// Handles both single-faced and double-faced cards.
        /// For DFCs, always returns the front face image.
        /// Returns null if no image is available.
        /// </summary>
        public string? GetImageUrl(string size = "large")
        {
            // Standard single-faced card
            if (ImageUris != null)
                return size == "normal" ? ImageUris.Normal : ImageUris.Large;

            // Double-faced card: use the front face image
            if (CardFaces?.Count > 0 && CardFaces[0].ImageUris != null)
                return size == "normal" ? CardFaces[0].ImageUris!.Normal : CardFaces[0].ImageUris!.Large;

            return null;
        }
    }

    /// <summary>
    /// Image URLs for a card or card face.
    /// Scryfall provides multiple sizes; we use "normal" for previews and "large" for PDF generation.
    /// </summary>
    public class ImageUris
    {
        /// <summary>Normal size image (~488x680px). Used for previews in the UI.</summary>
        [JsonProperty("normal")]
        public string? Normal { get; set; }

        /// <summary>Large size image (~672x936px). Used for high-quality PDF generation.</summary>
        [JsonProperty("large")]
        public string? Large { get; set; }
    }

    /// <summary>
    /// Represents one face of a double-faced card (DFC).
    /// Each face has its own name and image.
    /// </summary>
    public class CardFace
    {
        /// <summary>Name of this face (e.g. "Delver of Secrets").</summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>Image URLs for this face. May be null for some card types.</summary>
        [JsonProperty("image_uris")]
        public ImageUris? ImageUris { get; set; }
    }

    /// <summary>
    /// Represents a single entry in the deck list.
    /// Holds the card name, quantity, and the resolved Scryfall printing.
    /// </summary>
    public class DeckEntry
    {
        /// <summary>Number of copies of this card in the deck.</summary>
        public int Quantity { get; set; }

        /// <summary>Card name as entered by the user in the deck list.</summary>
        public string CardName { get; set; } = "";

        /// <summary>Optional set code parsed from the deck list (e.g. "M21").</summary>
        public string? SetCode { get; set; }

        /// <summary>Optional collector number parsed from the deck list (e.g. "152").</summary>
        public string? CollectorNumber { get; set; }

        /// <summary>All available printings of this card loaded from Scryfall.</summary>
        public List<ScryfallCard> AvailablePrints { get; set; } = new();

        /// <summary>The printing selected by the user (or auto-selected on load).</summary>
        public ScryfallCard? SelectedPrint { get; set; }

        /// <summary>True if a printing has been successfully selected.</summary>
        public bool IsLoaded => SelectedPrint != null;

        /// <summary>True while the card is being searched on Scryfall.</summary>
        public bool IsLoading { get; set; }

        /// <summary>True if the Scryfall search failed for this card.</summary>
        public bool HasError { get; set; }

        /// <summary>Error message shown in the UI when HasError is true.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Status text displayed under the card name in the deck list.
        /// Shows error, loading state, edition info, or pending state.
        /// </summary>
        public string StatusText
        {
            get
            {
                if (HasError) return $"{ErrorMessage}";
                if (IsLoading) return "Loading...";
                if (IsLoaded) return $"{SelectedPrint!.DisplayInfo}";
                return "Pending";
            }
        }
    }
}
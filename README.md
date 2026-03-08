# MtgProxyPrinter

A desktop application for Windows that generates print-ready PDF proxy sheets for Magic: The Gathering cards. Built with WPF and .NET 9, it uses the Scryfall API to search and download card images automatically.

## What it does

MtgProxyPrinter takes a standard deck list, searches Scryfall for each card, and produces a PDF ready to print and cut. Each page fits 9 cards in a 3x3 grid sized to standard MTG card dimensions (6.35 x 8.89 cm) on A4 paper.

Cards are downloaded once and cached locally, so regenerating a PDF for the same deck is fast.

## Features

- Paste any standard deck list and load all cards with a single click
- Preferred language support with automatic English fallback (supports es, en, fr, de, it, pt, ja, ko, ru, zhs, zht)
- Art selector to choose from all available printings of each card across every set and language
- Double-faced card support (front face is used for the PDF)
- Local image cache to avoid redundant downloads
- Progress bar and status messages during card loading and PDF generation
- UI available in Spanish and English, switchable at runtime
- Sample deck loader for quick testing

## Requirements

- Windows 10 or later
- .NET 9 Desktop Runtime
- Internet connection (for Scryfall image downloads)

## How to use

1. Paste your deck list into the text area. Supported formats:

```
4 Lightning Bolt
1 Sol Ring (ECC) 57
1 Counterspell (DSC) 114
```

Each line can optionally include a set code and collector number in parentheses to target a specific printing.

2. Select your preferred card language from the dropdown.
3. Click "Load Cards". The app searches Scryfall for each card and shows a preview.
4. Optionally click any card to open the art selector and choose a different printing.
5. Click "Generate PDF", choose a save location, and the file is ready to print.

## Architecture

The project follows the MVVM pattern using CommunityToolkit.Mvvm.

- `MainViewModel` manages the deck list state, card loading, and PDF generation commands
- `ScryfallService` handles all Scryfall API communication with rate limiting (max 10 req/s)
- `PdfGeneratorService` builds the PDF using PdfSharp, downloading and caching images as needed
- `LocalizationService` handles runtime language switching via ResourceDictionary swapping

## Dependencies

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [PdfSharp](http://www.pdfsharp.net/)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [Scryfall API](https://scryfall.com/docs/api)

## Notes

This tool is intended for personal use to print proxies for casual play, playtesting, or collection purposes. Please respect the Scryfall API usage policy and do not abuse the rate limiter.

Card images and card names are property of Wizards of the Coast.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;
using NickeltownPOSV4.Services.Migration;

namespace NickeltownPOSV4.Services.Stock;

public sealed class StockV2ItemImportPreviewService
{
    private readonly IMigrationFingerprintStore _fingerprints;
    private readonly IItemMigrationRepository _items;

    private static readonly IReadOnlyList<StockImportFieldMappingRow> FieldMappings =
    [
        new("Name", "Items.Name", true),
        new("Sku / Barcode / Barcodes[]", "Items.Sku + AlternateSkusJson", true),
        new("Price", "ItemPrices.Bar", false),
        new("GuestPrice", "ItemPrices.Guest", false),
        new("PitstopPrice", "ItemPrices.Pitstop", false),
        new("ShowInBar", "Items.ShowInBar", false),
        new("ShowInPitstop", "Items.ShowInPitstop", false),
        new("IsSharedItem", "Items.IsSharedItem + CatalogBucket", false),
        new("OrderIn", "Items.OrderInMerchandise / StockMode", false),
        new("DisableStockTracking / TrackStock", "Items.TrackStock / StockMode", false),
        new("StockMode", "Items.StockMode", false),
        new("StockCount / Stock / Quantity", "Items.StockQty", false),
        new("LowStockThreshold", "Items.LowStockThreshold", false),
        new("ParLevel", "ItemDescription JSON (parLevel)", false),
        new("SpecialPrice", "ItemPrices.BarSpecial", false),
        new("SpecialGuestPrice", "ItemPrices.GuestSpecial", false),
        new("SpecialPitstopPrice", "ItemPrices.PitstopSpecial", false),
        new("IsOnSpecial", "Items.IsOnSpecial", false),
        new("SourceSystem", "ItemDescription JSON (sourceSystem)", false),
        new("Category / SubCategory", "CatalogBucket + CatalogSubCategory", false),
        new("ImagePath", "Items.ImagePath", false),
        new("Description", "ItemDescription (notes)", false),
    ];

    public StockV2ItemImportPreviewService(
        IMigrationFingerprintStore fingerprints,
        IItemMigrationRepository items)
    {
        _fingerprints = fingerprints;
        _items = items;
    }

    public IReadOnlyList<StockImportFieldMappingRow> GetFieldMappings() => FieldMappings;

    public async Task<StockV2ImportFilePreview> PreviewFileForImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var hash = await LegacyJsonDocumentReader.ComputeSha256HexAsync(filePath, cancellationToken).ConfigureAwait(false);
        var alreadyImported = await _fingerprints
            .WasSuccessfullyImportedAsync(LegacyJsonFileKind.Items, filePath, hash, cancellationToken)
            .ConfigureAwait(false);
        var items = ParseStockItemsJson(json);
        var preview = BuildPreview(items);
        if (alreadyImported)
        {
            var dupWarnings = new List<StockV2ImportIssue>(preview.Warnings)
            {
                new(
                    "(file)",
                    "This exact stock_items.json was already imported successfully. Re-importing may duplicate or overwrite items depending on legacy IDs.",
                    StockV2ImportIssueSeverity.Warning),
            };
            preview = new StockV2ImportPreviewResult(preview.ValidCount, dupWarnings, preview.Errors);
        }

        return new StockV2ImportFilePreview(preview, alreadyImported, filePath, hash);
    }

    public async Task<StockV2ImportPreviewResult> PreviewFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var wrapped = await PreviewFileForImportAsync(filePath, cancellationToken).ConfigureAwait(false);
        return wrapped.Result;
    }

    public async Task CommitImportAsync(
        string filePath,
        string contentSha256Hex,
        IReadOnlyList<LegacyItemDto> items,
        CancellationToken cancellationToken = default)
    {
        await _items.ImportItemsAsync(items, cancellationToken).ConfigureAwait(false);
        await _fingerprints
            .MarkSuccessfullyImportedAsync(LegacyJsonFileKind.Items, filePath, contentSha256Hex, cancellationToken)
            .ConfigureAwait(false);
    }

    public static List<LegacyItemDto> ParseStockItemsJson(string json)
    {
        var items = new List<LegacyItemDto>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var model = JsonSerializer.Deserialize<LegacyItemDto>(
                element.GetRawText(),
                MigrationJsonDefaults.SerializerOptions);
            if (model is null)
            {
                continue;
            }

            LegacyDtoCoalescing.ApplyLooseItemFields(model, element);
            LegacyDtoCoalescing.FinalizePosBarStockItem(model);
            items.Add(model);
        }

        return items;
    }

    public StockV2ImportPreviewResult BuildPreview(IReadOnlyList<LegacyItemDto> items)
    {
        var warnings = new List<StockV2ImportIssue>();
        var errors = new List<StockV2ImportIssue>();
        var valid = 0;
        var barcodeIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in items)
        {
            var name = string.IsNullOrWhiteSpace(dto.Name) ? "(unnamed)" : dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add(new StockV2ImportIssue(name, "Name is required.", StockV2ImportIssueSeverity.Error));
                continue;
            }

            LegacyItemImportMapper.ItemImportRow mapped;
            try
            {
                var legacyId = string.IsNullOrWhiteSpace(dto.Id) ? LegacyStableId.ForItem(dto) : dto.Id!;
                mapped = LegacyItemImportMapper.Map(dto, legacyId);
            }
            catch (Exception ex)
            {
                errors.Add(new StockV2ImportIssue(name, ex.Message, StockV2ImportIssueSeverity.Error));
                continue;
            }

            valid++;

            var (expBar, expPit) = StockCatalogTaxonomy.ExpectedVisibilityForBucket(mapped.CatalogBucket);
            if (mapped.ShowInBar != expBar || mapped.ShowInPitstop != expPit)
            {
                warnings.Add(new StockV2ImportIssue(
                    name,
                    $"ShowInBar/ShowInPitstop ({mapped.ShowInBar}/{mapped.ShowInPitstop}) don't match bucket \"{mapped.CatalogBucket}\" "
                    + $"(expected {expBar}/{expPit}). Saving in Stock Management uses bucket rules.",
                    StockV2ImportIssueSeverity.Warning));
            }

            foreach (var code in EnumerateBarcodes(dto))
            {
                if (barcodeIndex.TryGetValue(code, out var other))
                {
                    warnings.Add(new StockV2ImportIssue(
                        name,
                        $"Duplicate barcode \"{code}\" (also on {other}).",
                        StockV2ImportIssueSeverity.Warning));
                }
                else
                {
                    barcodeIndex[code] = name;
                }
            }

            var expectsPitstop = dto.ShowInPitstop == true
                || (dto.ShowInPitstop is null && dto.PitstopPrice is { } pitHint && pitHint > 0m);
            if (expectsPitstop && (!(dto.PitstopPrice is { } pitPrice) || pitPrice <= 0m))
            {
                warnings.Add(new StockV2ImportIssue(name, "Missing PitstopPrice.", StockV2ImportIssueSeverity.Warning));
            }

            if (dto.OrderIn == true)
            {
                warnings.Add(new StockV2ImportIssue(name, "Marked Order In.", StockV2ImportIssueSeverity.Warning));
            }

            if (string.IsNullOrWhiteSpace(dto.Category)
                && string.IsNullOrWhiteSpace(dto.SubCategory)
                && string.IsNullOrWhiteSpace(dto.CategoryId))
            {
                warnings.Add(new StockV2ImportIssue(name, "Invalid or missing category.", StockV2ImportIssueSeverity.Warning));
            }

            if (string.IsNullOrWhiteSpace(dto.StockMode)
                && dto.DisableStockTracking is null
                && dto.TrackStock is null
                && dto.TrackInventory is null
                && dto.OrderIn is null)
            {
                warnings.Add(new StockV2ImportIssue(name, "Missing stock mode / tracking flags.", StockV2ImportIssueSeverity.Warning));
            }

            var trackOn = dto.DisableStockTracking != true
                && dto.TrackStock != false
                && dto.TrackInventory != false;
            if (dto.OrderIn == true && trackOn)
            {
                warnings.Add(new StockV2ImportIssue(
                    name,
                    "Track stock enabled but item is Order In.",
                    StockV2ImportIssueSeverity.Warning));
            }

            if (dto.IsOnSpecial == true && dto.SpecialPrice is not { } && dto.SpecialPitstopPrice is not { })
            {
                warnings.Add(new StockV2ImportIssue(name, "On special but no special prices set.", StockV2ImportIssueSeverity.Warning));
            }
        }

        return new StockV2ImportPreviewResult(valid, warnings, errors);
    }

    private static IEnumerable<string> EnumerateBarcodes(LegacyItemDto dto)
    {
        foreach (var code in new[] { dto.Sku, dto.Barcode, dto.ProductCode })
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                yield return code.Trim();
            }
        }

        if (dto.Barcodes is null)
        {
            yield break;
        }

        foreach (var b in dto.Barcodes)
        {
            if (!string.IsNullOrWhiteSpace(b))
            {
                yield return b.Trim();
            }
        }
    }
}

public sealed record StockImportFieldMappingRow(string JsonField, string AppField, bool Required);

public enum StockV2ImportIssueSeverity
{
    Warning,
    Error,
}

public sealed record StockV2ImportIssue(string ItemName, string Message, StockV2ImportIssueSeverity Severity);

public sealed record StockV2ImportPreviewResult(
    int ValidCount,
    IReadOnlyList<StockV2ImportIssue> Warnings,
    IReadOnlyList<StockV2ImportIssue> Errors)
{
    public int WarningCount => Warnings.Count;

    public int ErrorCount => Errors.Count;
}

public sealed record StockV2ImportFilePreview(
    StockV2ImportPreviewResult Result,
    bool AlreadyImported,
    string SourcePath,
    string ContentSha256Hex);

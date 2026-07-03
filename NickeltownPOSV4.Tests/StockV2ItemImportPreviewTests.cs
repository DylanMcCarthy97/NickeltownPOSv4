using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;
using NickeltownPOSV4.Services.Stock;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class StockV2ItemImportPreviewTests
{
    [Fact]
    public void BuildPreview_WarnsWhenVisibilityFlagsMismatchBucket()
    {
        var dto = new LegacyItemDto
        {
            Id = "item-1",
            Name = "Mismatch Lager",
            Category = "Bar",
            ShowInBar = false,
            ShowInPitstop = true,
            Price = 5m,
        };

        var svc = new StockV2ItemImportPreviewService(new StubFingerprintStore(), new StubItemRepository());
        var result = svc.BuildPreview([dto]);

        Assert.Equal(1, result.ValidCount);
        Assert.Contains(
            result.Warnings,
            w => w.ItemName == "Mismatch Lager" && w.Message.Contains("don't match bucket", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExpectedVisibilityForBucket_MatchesCatalogRules()
    {
        Assert.Equal((1, 0), StockCatalogTaxonomy.ExpectedVisibilityForBucket(StockCatalogTaxonomy.BucketBar));
        Assert.Equal((0, 1), StockCatalogTaxonomy.ExpectedVisibilityForBucket(StockCatalogTaxonomy.BucketPitstop));
        Assert.Equal((1, 1), StockCatalogTaxonomy.ExpectedVisibilityForBucket(StockCatalogTaxonomy.BucketShared));
    }

    private sealed class StubFingerprintStore : IMigrationFingerprintStore
    {
        public Task<bool> WasSuccessfullyImportedAsync(
            LegacyJsonFileKind kind,
            string sourcePath,
            string contentSha256Hex,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task MarkSuccessfullyImportedAsync(
            LegacyJsonFileKind kind,
            string sourcePath,
            string contentSha256Hex,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubItemRepository : IItemMigrationRepository
    {
        public Task ImportItemsAsync(IReadOnlyList<LegacyItemDto> items, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

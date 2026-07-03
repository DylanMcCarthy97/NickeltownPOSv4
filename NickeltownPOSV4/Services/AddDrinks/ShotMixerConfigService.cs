using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.AddDrinks;

public sealed class ShotMixerConfigService : IShotMixerConfigService
{
    private const decimal DefaultShotPrice = 4.00m;

    private readonly SqliteConnectionFactory _factory;

    private ShotMixerPriceRow? _cachedRow;

    public ShotMixerConfigService(SqliteConnectionFactory factory) => _factory = factory;

    public void Invalidate() => _cachedRow = null;

    public Task EnsureExistsAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => EnsureExistsCore(cancellationToken), cancellationToken);

    public Task<ShotMixerRuntimeConfig> GetAsync(bool isGuestTab, CancellationToken cancellationToken = default) =>
        Task.Run(() => BuildRuntimeConfig(isGuestTab, cancellationToken), cancellationToken);

    private void EnsureExistsCore(CancellationToken cancellationToken)
    {
        using var conn = _factory.OpenConnection();
        var existing = conn.QuerySingleOrDefault<long?>(
            new CommandDefinition(
                """
                SELECT Id FROM Items
                WHERE COALESCE(IsActive, 1) != 0
                  AND (
                    lower(trim(COALESCE(ItemType, ''))) = lower(@type)
                    OR lower(trim(Name)) LIKE '%shot + mixer%'
                  )
                ORDER BY Id
                LIMIT 1
                """,
                new { type = ShotMixerCatalog.ItemType },
                cancellationToken: cancellationToken));

        if (existing is > 0)
        {
            return;
        }

        var legacy = "v4_shot_mixer";
        var spiritsJson = ShotMixerSpiritsSerializer.ToStorageJson(ShotMixerSpiritsSerializer.Parse(null));
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO Items (
                  LegacyId, LegacyKey, Name, Sku, CategoryId, ItemType, StockQty, TrackStock,
                  ImagePath, RawJson, IsActive, CreatedAt, UpdatedAt,
                  CatalogBucket, CatalogSubCategory, StockMode,
                  ShowInBar, ShowInPitstop, OrderInMerchandise, UsesOpenPrice, ItemDescription)
                VALUES (
                  @LegacyId, @LegacyKey, @Name, NULL, NULL, @ItemType, 0, 0,
                  NULL, '{}', 1, datetime('now'), datetime('now'),
                  'Bar', 'Spirits', @StockMode,
                  0, 0, 0, 0, @Desc)
                """,
                new
                {
                    LegacyId = legacy,
                    LegacyKey = legacy,
                    Name = ShotMixerCatalog.ItemName,
                    ItemType = ShotMixerCatalog.ItemType,
                    StockMode = StockCatalogTaxonomy.StockModeNotTracked,
                    Desc = spiritsJson,
                },
                cancellationToken: cancellationToken));

        var id = conn.ExecuteScalar<long>("SELECT last_insert_rowid()");
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO ItemPrices (ItemId, PriceKind, Price, EffectiveFrom, CreatedAt)
                VALUES (@id, 'Bar', @price, datetime('now'), datetime('now'))
                """,
                new { id, price = DefaultShotPrice },
                cancellationToken: cancellationToken));

        Invalidate();
    }

    private ShotMixerRuntimeConfig BuildRuntimeConfig(bool isGuestTab, CancellationToken cancellationToken)
    {
        var row = LoadPriceRow(cancellationToken);
        var bar = ToMoney(row.BarPrice);
        var guest = ToMoney(row.GuestPrice);
        var barSpecial = ToMoney(row.BarSpecialPrice);
        var guestSpecial = ToMoney(row.GuestSpecialPrice);
        if (bar <= 0m)
        {
            bar = DefaultShotPrice;
        }

        var price = BarCatalogPriceResolver.GetUnitPriceForBarAdd(
            bar,
            guest,
            barSpecial,
            guestSpecial,
            row.IsOnSpecial,
            isGuestTab);
        if (price <= 0m)
        {
            price = DefaultShotPrice;
        }

        var roundedPrice = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        var showSpecial = CatalogSpecialPricing.ShouldShowBarSpecialPrice(
            usesOpenPrice: false,
            row.IsOnSpecial,
            bar,
            guest,
            barSpecial,
            guestSpecial,
            isGuestTab,
            out var regularUnitPrice,
            out _);

        return new ShotMixerRuntimeConfig
        {
            ConfigItemId = row.ItemId,
            ShotPrice = roundedPrice,
            RegularUnitPrice = decimal.Round(regularUnitPrice, 2, MidpointRounding.AwayFromZero),
            ShowSpecialPricing = showSpecial,
            Spirits = ShotMixerSpiritsSerializer.Parse(row.ItemDescription),
        };
    }

    private ShotMixerPriceRow LoadPriceRow(CancellationToken cancellationToken)
    {
        if (_cachedRow is not null)
        {
            return _cachedRow;
        }

        using var conn = _factory.OpenConnection();
        var row = conn.QuerySingleOrDefault<ShotMixerPriceRow>(
            new CommandDefinition(
                """
                SELECT
                  i.Id AS ItemId,
                  i.ItemDescription,
                  CAST(COALESCE((
                    SELECT ip2.Price
                    FROM ItemPrices ip2
                    WHERE ip2.ItemId = i.Id AND COALESCE(ip2.PriceKind, 'Bar') = 'Bar'
                    ORDER BY datetime(ip2.EffectiveFrom) DESC, ip2.Id DESC
                    LIMIT 1
                  ), 0) AS REAL) AS BarPrice,
                  CAST(COALESCE((
                    SELECT ipg.Price
                    FROM ItemPrices ipg
                    WHERE ipg.ItemId = i.Id AND lower(trim(COALESCE(ipg.PriceKind, ''))) = 'guest'
                    ORDER BY datetime(ipg.EffectiveFrom) DESC, ipg.Id DESC
                    LIMIT 1
                  ), 0) AS REAL) AS GuestPrice,
                  CAST(COALESCE((
                    SELECT ips.Price
                    FROM ItemPrices ips
                    WHERE ips.ItemId = i.Id AND lower(trim(COALESCE(ips.PriceKind, ''))) = 'barspecial'
                    ORDER BY datetime(ips.EffectiveFrom) DESC, ips.Id DESC
                    LIMIT 1
                  ), 0) AS REAL) AS BarSpecialPrice,
                  CAST(COALESCE((
                    SELECT ipgs.Price
                    FROM ItemPrices ipgs
                    WHERE ipgs.ItemId = i.Id AND lower(trim(COALESCE(ipgs.PriceKind, ''))) = 'guestspecial'
                    ORDER BY datetime(ipgs.EffectiveFrom) DESC, ipgs.Id DESC
                    LIMIT 1
                  ), 0) AS REAL) AS GuestSpecialPrice,
                  COALESCE(i.IsOnSpecial, 0) AS IsOnSpecial
                FROM Items i
                WHERE COALESCE(i.IsActive, 1) != 0
                  AND (
                    lower(trim(COALESCE(i.ItemType, ''))) = lower(@type)
                    OR lower(trim(i.Name)) LIKE '%shot + mixer%'
                  )
                ORDER BY i.Id
                LIMIT 1
                """,
                new { type = ShotMixerCatalog.ItemType },
                cancellationToken: cancellationToken));

        if (row is null)
        {
            EnsureExistsCore(cancellationToken);
            return LoadPriceRow(cancellationToken);
        }

        _cachedRow = row;
        return row;
    }

    private static decimal ToMoney(double value) =>
        decimal.Round((decimal)value, 2, MidpointRounding.AwayFromZero);

    private sealed class ShotMixerPriceRow
    {
        public long ItemId { get; set; }

        public string? ItemDescription { get; set; }

        public double BarPrice { get; set; }

        public double GuestPrice { get; set; }

        public double BarSpecialPrice { get; set; }

        public double GuestSpecialPrice { get; set; }

        public int IsOnSpecial { get; set; }
    }
}

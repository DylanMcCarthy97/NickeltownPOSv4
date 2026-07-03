using System;
using System.Collections.Generic;
using System.Linq;

namespace NickeltownPOSV4.Data.Sqlite;

public static class StockCatalogTaxonomy
{
    public const string BucketBar = "Bar";
    public const string BucketPitstop = "Pitstop";
    public const string BucketShared = "Shared";
    public const string StockModeTracked = "Tracked";
    public const string StockModeSellUntilGone = "SellUntilGone";
    public const string StockModeOrderIn = "OrderIn";
    public const string StockModeNotTracked = "NotTracked";
    public static readonly string[] BarSubCategories = { "Beer", "Wine", "Spirits", "Food", "Merch", "Drinks" };
    public static readonly string[] PitstopSubCategories = { "Food", "Drinks", "Merch" };
    public static readonly string[] SharedSubCategories = { "Drinks", "Merch" };

    public static string NormalizeBucket(string? bucket)
    {
        var b = (bucket ?? string.Empty).Trim();
        if (b.Equals(BucketPitstop, StringComparison.OrdinalIgnoreCase)) return BucketPitstop;
        if (b.Equals(BucketShared, StringComparison.OrdinalIgnoreCase)) return BucketShared;
        return BucketBar;
    }

    public static string DefaultSubCategory(string bucket) => SubCategoriesForBucket(NormalizeBucket(bucket)).First();

    public static IEnumerable<string> SubCategoriesForBucket(string bucket) => NormalizeBucket(bucket) switch
    {
        BucketPitstop => PitstopSubCategories,
        BucketShared => SharedSubCategories,
        _ => BarSubCategories,
    };

    public static string NormalizeSubCategory(string bucket, string? subCategory)
    {
        var allowed = SubCategoriesForBucket(bucket).ToList();
        var s = (subCategory ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(s)) return allowed[0];
        return allowed.FirstOrDefault(a => a.Equals(s, StringComparison.OrdinalIgnoreCase)) ?? MapFuzzySubCategory(s, allowed);
    }

    public static string CatalogDisplayName(string bucket, string subCategory) =>
        $"{NormalizeBucket(bucket)} / {NormalizeSubCategory(bucket, subCategory)}";

    /// <summary>Bar/Pitstop visibility flags implied by catalog bucket (matches stock management bucket picker).</summary>
    public static (int ShowInBar, int ShowInPitstop) ExpectedVisibilityForBucket(string? bucket) =>
        NormalizeBucket(bucket) switch
        {
            BucketPitstop => (0, 1),
            BucketShared => (1, 1),
            _ => (1, 0),
        };

    public static string NormalizeStockMode(string? mode)
    {
        var m = (mode ?? string.Empty).Trim();
        if (m.Equals(StockModeSellUntilGone, StringComparison.OrdinalIgnoreCase)) return StockModeSellUntilGone;
        if (m.Equals(StockModeOrderIn, StringComparison.OrdinalIgnoreCase)) return StockModeOrderIn;
        if (m.Equals(StockModeNotTracked, StringComparison.OrdinalIgnoreCase)) return StockModeNotTracked;
        return StockModeTracked;
    }

    public static string StockModeFromLegacyFlags(int trackStock, int orderInMerchandise, int notGonnaOrderBack)
    {
        if (trackStock == 0 && orderInMerchandise != 0) return StockModeOrderIn;
        if (trackStock == 0) return StockModeNotTracked;
        if (notGonnaOrderBack != 0) return StockModeSellUntilGone;
        return StockModeTracked;
    }

    /// <summary>True when the item keeps a shelf quantity (stock management controls and snapshot reports).</summary>
    public static bool MaintainsOnHandQuantity(int trackStock, int orderInMerchandise) =>
        trackStock != 0 && orderInMerchandise == 0;

    public static void ApplyStockModeToFlags(string stockMode, out int trackStock, out int orderInMerchandise, out int notGonnaOrderBack, out int includeInWeeklyStockReport, out int isRunOutItem)
    {
        switch (NormalizeStockMode(stockMode))
        {
            case StockModeSellUntilGone:
                trackStock = 1; orderInMerchandise = 0; notGonnaOrderBack = 1; includeInWeeklyStockReport = 1; isRunOutItem = 1;
                break;
            case StockModeOrderIn:
                trackStock = 0; orderInMerchandise = 1; notGonnaOrderBack = 0; includeInWeeklyStockReport = 0; isRunOutItem = 0;
                break;
            case StockModeNotTracked:
                trackStock = 0; orderInMerchandise = 0; notGonnaOrderBack = 0; includeInWeeklyStockReport = 0; isRunOutItem = 0;
                break;
            default:
                trackStock = 1; orderInMerchandise = 0; notGonnaOrderBack = 0; includeInWeeklyStockReport = 1; isRunOutItem = 0;
                break;
        }
    }

    public static IReadOnlyList<string> AllFilterCategoryLabels()
    {
        var list = new List<string> { "All", BucketBar, BucketPitstop, BucketShared };
        foreach (var b in new[] { BucketBar, BucketPitstop, BucketShared })
        foreach (var sub in SubCategoriesForBucket(b))
            list.Add(CatalogDisplayName(b, sub));
        return list;
    }

    public static void InferFromLegacy(string? legacyCategoryName, bool showInBar, bool showInPitstop, out string bucket, out string subCategory)
    {
        var name = (legacyCategoryName ?? string.Empty).Trim();
        bucket = InferBucketFromNameAndVisibility(name, showInBar, showInPitstop);
        subCategory = InferSubFromLegacyName(name, bucket);
    }

    public static bool IsBarLineFood(string bucket, string subCategory) =>
        (NormalizeBucket(bucket) == BucketBar || NormalizeBucket(bucket) == BucketPitstop)
        && NormalizeSubCategory(bucket, subCategory).Equals("Food", StringComparison.OrdinalIgnoreCase);

    public static bool SkipsBarStockDecrement(string bucket, string subCategory) =>
        IsBarLineFood(bucket, subCategory) || IsLegacyFoodSubLabel(subCategory);

    private static bool IsLegacyFoodSubLabel(string subCategory)
    {
        var food = new[] { "Snacks", "Meals", "Appetizers", "Legacy Food", "Meal Deal" };
        return food.Contains(subCategory, StringComparer.OrdinalIgnoreCase);
    }

    private static string InferBucketFromNameAndVisibility(string name, bool showInBar, bool showInPitstop)
    {
        if (name.Contains("shared", StringComparison.OrdinalIgnoreCase) || name.Contains("both", StringComparison.OrdinalIgnoreCase) || (showInBar && showInPitstop))
            return BucketShared;
        if (name.Contains("pitstop", StringComparison.OrdinalIgnoreCase) || (!showInBar && showInPitstop))
            return BucketPitstop;
        return BucketBar;
    }

    private static string InferSubFromLegacyName(string name, string bucket)
    {
        var allowed = SubCategoriesForBucket(bucket).ToList();
        if (string.IsNullOrWhiteSpace(name)) return DefaultSubCategory(bucket);
        foreach (var sub in allowed)
            if (name.Contains(sub, StringComparison.OrdinalIgnoreCase)) return sub;
        if (name.Contains("beer", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Beer");
        if (name.Contains("wine", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Wine");
        if (name.Contains("spirit", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Spirits");
        if (name.Contains("merch", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Merch");
        if (name.Contains("food", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Food");
        if (name.Contains("drink", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Drinks");
        var slash = name.IndexOf('/');
        if (slash >= 0 && slash < name.Length - 1) return NormalizeSubCategory(bucket, name[(slash + 1)..].Trim());
        return DefaultSubCategory(bucket);
    }

    private static string PickOrDefault(IReadOnlyList<string> allowed, string preferred) =>
        allowed.FirstOrDefault(a => a.Equals(preferred, StringComparison.OrdinalIgnoreCase)) ?? allowed[0];

    private static string MapFuzzySubCategory(string raw, IReadOnlyList<string> allowed)
    {
        if (raw.Contains("beer", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Beer");
        if (raw.Contains("wine", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Wine");
        if (raw.Contains("spirit", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Spirits");
        if (raw.Contains("merch", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Merch");
        if (raw.Contains("food", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Food");
        if (raw.Contains("drink", StringComparison.OrdinalIgnoreCase)) return PickOrDefault(allowed, "Drinks");
        return allowed[0];
    }
}

/// <summary>Shared SQL for stock snapshot CSV/PDF exports.</summary>
internal static class StockSnapshotQuery
{
    internal const string CategorySelectExpr =
        "trim(COALESCE(NULLIF(TRIM(i.CatalogBucket), ''), 'Bar'))"
        + " || ' / ' || "
        + "trim(COALESCE(NULLIF(TRIM(i.CatalogSubCategory), ''), 'Drinks'))";

    internal const string ReportWhereClause =
        """
        WHERE COALESCE(i.IsActive, 1) = 1
          AND COALESCE(i.IncludeInWeeklyStockReport, 1) != 0
        """;

    internal const string OrderByClause =
        """
        ORDER BY
          lower(trim(COALESCE(i.CatalogBucket, ''))) COLLATE NOCASE,
          lower(trim(COALESCE(i.CatalogSubCategory, ''))) COLLATE NOCASE,
          i.Name COLLATE NOCASE
        """;

    internal static int EffectiveLowStockThreshold(int? value) => value is > 0 ? value.Value : 5;
}

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Two independent shopping lists — never mixed in generation or Saturday email.</summary>
public enum ShoppingListKind
{
    /// <summary>Beer, wine, spirits, soft drinks / water (CatalogSubCategory). Excludes Merch and Food.</summary>
    Bar,

    /// <summary>Club merchandise (CatalogSubCategory == Merch).</summary>
    Merchandise,
}

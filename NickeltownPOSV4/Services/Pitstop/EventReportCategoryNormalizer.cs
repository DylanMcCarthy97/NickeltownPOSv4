using System;

namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>Normalizes product categories for combined Pitstop event reports.</summary>
public static class EventReportCategoryNormalizer
{
    public const string Food = "Food";
    public const string Drinks = "Drinks";
    public const string Merchandise = "Merchandise";
    public const string Memberships = "Memberships";
    public const string Other = "Other";

    public static string Normalize(string? categoryName, string? productName)
    {
        var name = (productName ?? string.Empty).Trim();
        if (name.Contains("membership", StringComparison.OrdinalIgnoreCase))
        {
            return Memberships;
        }

        var category = (categoryName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(category))
        {
            return InferFromProductName(name);
        }

        if (category.Contains("membership", StringComparison.OrdinalIgnoreCase))
        {
            return Memberships;
        }

        if (category.Contains("food", StringComparison.OrdinalIgnoreCase)
            || category.Contains("snack", StringComparison.OrdinalIgnoreCase)
            || category.Contains("meal", StringComparison.OrdinalIgnoreCase))
        {
            return Food;
        }

        if (category.Contains("drink", StringComparison.OrdinalIgnoreCase)
            || category.Contains("beer", StringComparison.OrdinalIgnoreCase)
            || category.Contains("wine", StringComparison.OrdinalIgnoreCase)
            || category.Contains("spirit", StringComparison.OrdinalIgnoreCase))
        {
            return Drinks;
        }

        if (category.Contains("merch", StringComparison.OrdinalIgnoreCase))
        {
            return Merchandise;
        }

        if (category.Equals(Food, StringComparison.OrdinalIgnoreCase))
        {
            return Food;
        }

        if (category.Equals(Drinks, StringComparison.OrdinalIgnoreCase))
        {
            return Drinks;
        }

        if (category.Equals("Merch", StringComparison.OrdinalIgnoreCase)
            || category.Equals(Merchandise, StringComparison.OrdinalIgnoreCase))
        {
            return Merchandise;
        }

        return InferFromProductName(name);
    }

    private static string InferFromProductName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Other;
        }

        if (name.Contains("membership", StringComparison.OrdinalIgnoreCase))
        {
            return Memberships;
        }

        if (name.Contains("shirt", StringComparison.OrdinalIgnoreCase)
            || name.Contains("cap", StringComparison.OrdinalIgnoreCase)
            || name.Contains("merch", StringComparison.OrdinalIgnoreCase))
        {
            return Merchandise;
        }

        return Other;
    }
}

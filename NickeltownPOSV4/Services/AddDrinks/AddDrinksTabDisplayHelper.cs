using System.Globalization;

namespace NickeltownPOSV4.Services.AddDrinks;

internal static class AddDrinksTabDisplayHelper
{
    public static string FormatTargetTabTitle(string? tabLegacyId, string? tabDisplayName)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId))
        {
            return "No tab selected — tap a member tab on the board or a guest under Guests.";
        }

        var name = string.IsNullOrWhiteSpace(tabDisplayName) ? tabLegacyId : tabDisplayName;
        return $"Tab: {name}";
    }

    public static string FormatWorkspaceTabDisplayName(string? tabLegacyId, string? tabDisplayName) =>
        string.IsNullOrWhiteSpace(tabLegacyId)
            ? "No tab selected"
            : (string.IsNullOrWhiteSpace(tabDisplayName) ? tabLegacyId! : tabDisplayName!);

    public static string FormatWorkspaceBalanceDisplay(
        decimal? currentBalance,
        decimal cartSubtotal,
        bool isCartEmpty)
    {
        if (currentBalance is not decimal bal)
        {
            return "Balance —";
        }

        var balText = bal.ToString("C2", CultureInfo.CurrentCulture);
        if (isCartEmpty)
        {
            return $"Balance {balText}";
        }

        var projected = bal - cartSubtotal;
        var projText = projected.ToString("C2", CultureInfo.CurrentCulture);
        return $"Balance {balText}  →  {projText} after add";
    }
}

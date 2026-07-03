using System.Globalization;

namespace NickeltownPOSV4.Models;

public sealed record ArchivedTabListItem(
    string LegacyId,
    string DisplayLabel,
    decimal Balance,
    string LastActivityText)
{
    public string BalanceLine =>
        "$" + Balance.ToString("0.00", CultureInfo.InvariantCulture);
}

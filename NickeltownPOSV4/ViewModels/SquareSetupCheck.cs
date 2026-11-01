namespace NickeltownPOSV4.ViewModels;

/// <summary>
/// Staff-facing health-check copy for Square shop vs reader.
/// A missing shop ID does not block card charges when the reader is already set.
/// </summary>
public static class SquareSetupCheck
{
    public static SystemCheckRowVm ShopRow(string? locationId, bool readerIsSet)
    {
        if (!string.IsNullOrWhiteSpace(locationId))
        {
            return new SystemCheckRowVm("Shop ID", "Set", SystemCheckSeverity.Ok);
        }

        return new SystemCheckRowVm(
            "Shop ID",
            "Not set",
            readerIsSet ? SystemCheckSeverity.Warning : SystemCheckSeverity.Error,
            readerIsSet
                ? "Cards still work. A manager can add the shop ID under Card reader settings."
                : "Card payments need the shop ID. Ask a manager to open Card reader settings.");
    }
}

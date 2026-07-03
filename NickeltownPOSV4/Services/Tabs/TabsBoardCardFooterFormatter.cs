namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsBoardCardFooterFormatter
{
    public static string Format(int openDrinkCount, string? lastActivityAt)
    {
        var drinks = openDrinkCount == 1 ? "1 drink" : $"{openDrinkCount} drinks";
        var activity = FormatLastActivity(lastActivityAt);
        return $"\U0001F37A {drinks} \u2022 Last activity: {activity}";
    }

    private static string FormatLastActivity(string? stamp)
    {
        var relative = TabsBoardActivityFormatter.FormatRelative(stamp);
        return relative == "-" ? "unknown" : relative;
    }
}
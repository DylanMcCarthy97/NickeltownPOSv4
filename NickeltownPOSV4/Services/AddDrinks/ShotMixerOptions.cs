namespace NickeltownPOSV4.Services.AddDrinks;

internal static class ShotMixerOptions
{
    public static string FormatLineName(string spirit, string mixerName) => $"{spirit} + {mixerName}";
}

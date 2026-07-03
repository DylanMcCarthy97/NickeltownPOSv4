namespace NickeltownPOSV4.ViewModels;

public sealed class MixerPickerChoice
{
    public MixerPickerChoice(long itemId, string name, string priceText, string stockText, bool isEnabled)
    {
        ItemId = itemId;
        Name = name;
        PriceText = priceText;
        StockText = stockText;
        IsEnabled = isEnabled;
    }

    public long ItemId { get; }

    public string Name { get; }

    public string PriceText { get; }

    public string StockText { get; }

    public bool IsEnabled { get; }

    public string SubtitleText => string.IsNullOrEmpty(PriceText) ? StockText : $"{PriceText}  ·  {StockText}";

    public override string ToString() => $"{Name}  —  {SubtitleText}";
}

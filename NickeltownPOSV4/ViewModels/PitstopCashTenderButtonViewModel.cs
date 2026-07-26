namespace NickeltownPOSV4.ViewModels;

/// <summary>One till quick-tender / note chip on the Pitstop cash pad.</summary>
public sealed class PitstopCashTenderButtonViewModel
{
    public PitstopCashTenderButtonViewModel(string label, decimal amount, bool isExact, bool addsToTender)
    {
        Label = label;
        Amount = amount;
        IsExact = isExact;
        AddsToTender = addsToTender;
    }

    public string Label { get; }

    public decimal Amount { get; }

    public bool IsExact { get; }

    /// <summary>When true, tapping adds this note onto the current received amount.</summary>
    public bool AddsToTender { get; }
}
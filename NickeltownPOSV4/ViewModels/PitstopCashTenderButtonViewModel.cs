namespace NickeltownPOSV4.ViewModels;

/// <summary>One till quick-tender chip on the Pitstop cash pad.</summary>
public sealed class PitstopCashTenderButtonViewModel
{
    public PitstopCashTenderButtonViewModel(string label, decimal amount, bool isExact)
    {
        Label = label;
        Amount = amount;
        IsExact = isExact;
    }

    public string Label { get; }

    public decimal Amount { get; }

    public bool IsExact { get; }
}
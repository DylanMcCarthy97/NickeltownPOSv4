namespace NickeltownPOSV4.Services.Pitstop;

public enum PitstopCardFixStep
{
    AskCheck,
    Checking,
    Saving,
    Paid,
    NotCharged,
    Cancelled,
    Unknown,
    Saved,
}

public enum CardFixOpenKind
{
    None,
    Recovery,
    NotCharged,
}

public sealed class PitstopCardFixCopy
{
    public required string Title { get; init; }

    public required string Body { get; init; }

    public required string AmountText { get; init; }

    public bool ShowCheck { get; init; }

    public bool ShowSaveSale { get; init; }

    public bool ShowNotCharged { get; init; }

    public bool ShowOk { get; init; }

    public bool ShowRetryCard { get; init; }

    public bool ShowTakeCash { get; init; }

    public string CheckLabel { get; init; } = "Find out if they paid";

    public string SaveLabel { get; init; } = "Save the sale";

    public string NotChargedLabel { get; init; } = "They weren't charged";

    public string OkLabel { get; init; } = "OK — keep selling";

    public string RetryCardLabel { get; init; } = "Try the card again";

    public string TakeCashLabel { get; init; } = "Take cash";
}

/// <summary>
/// Plain-English next step after a Pitstop card payment does not finish.
/// No Square IDs, no Admin, no PIN.
/// </summary>
public static class PitstopCardFixGuide
{
    public static PitstopCardFixCopy Build(
        PitstopCardFixStep step,
        decimal amount,
        bool canRebuildSale,
        bool canAskReader = true)
    {
        var money = amount.ToString("C2");
        return step switch
        {
            PitstopCardFixStep.Paid => new PitstopCardFixCopy
            {
                Title = "They paid",
                Body = canRebuildSale
                    ? "The card went through. Tap Save the sale. Do not charge them again."
                    : "The card went through, but this till cannot rebuild the sale. Write down the amount. Do not take cash for this same sale.",
                AmountText = money,
                ShowSaveSale = canRebuildSale,
                ShowOk = !canRebuildSale,
                OkLabel = canRebuildSale ? "OK — keep selling" : "I've written it down",
            },
            PitstopCardFixStep.Checking => new PitstopCardFixCopy
            {
                Title = "Checking if they paid",
                Body = "Asking the card reader. This does not charge anyone.",
                AmountText = money,
            },
            PitstopCardFixStep.Saving => new PitstopCardFixCopy
            {
                Title = "Saving the sale",
                Body = "The card went through. Saving it now. Do not charge them again.",
                AmountText = money,
            },
            PitstopCardFixStep.NotCharged => new PitstopCardFixCopy
            {
                Title = "They weren't charged",
                Body = "No money was taken. This sale is still on the till. Try the card again, take cash, or keep the sale.",
                AmountText = money,
                ShowRetryCard = true,
                ShowTakeCash = true,
                ShowOk = true,
                OkLabel = "Keep the sale",
            },
            PitstopCardFixStep.Cancelled => new PitstopCardFixCopy
            {
                Title = "Cancelled",
                Body = "Nothing was taken. The sale is still here.",
                AmountText = money,
                ShowOk = true,
                OkLabel = "Keep the sale",
            },
            PitstopCardFixStep.Saved => new PitstopCardFixCopy
            {
                Title = "Sale saved",
                Body = "The sale is in the till. Do not charge them again.",
                AmountText = money,
                ShowOk = true,
            },
            PitstopCardFixStep.Unknown => new PitstopCardFixCopy
            {
                Title = "We still can't tell",
                Body = canAskReader
                    ? "Look at the card reader. If it says approved, tap Find out if they paid again. If it says cancelled or nothing happened, tap They weren't charged."
                    : "Look at the card reader. If it says cancelled or nothing happened, tap They weren't charged. If it says approved, write down the amount. Do not charge them again.",
                AmountText = money,
                ShowCheck = canAskReader,
                ShowNotCharged = true,
            },
            _ => new PitstopCardFixCopy
            {
                Title = "This card payment didn't finish",
                Body = "Do not run the card again. Tap Find out if they paid. That checks the reader — it does not charge anyone.",
                AmountText = money,
                ShowCheck = true,
            },
        };
    }

    public static PitstopCardFixStep OpeningStep(bool theyPaid, bool hasCheckoutId)
    {
        if (theyPaid)
        {
            return PitstopCardFixStep.Paid;
        }

        // A leftover with no checkout ID cannot be asked — that is what locked the till.
        return hasCheckoutId ? PitstopCardFixStep.AskCheck : PitstopCardFixStep.Unknown;
    }

    public static PitstopCardFixStep AfterSquareCheck(
        bool approved,
        bool confirmedNotPaid,
        bool canRebuildSale,
        bool cancelled = false)
    {
        if (approved)
        {
            return PitstopCardFixStep.Paid;
        }

        if (cancelled)
        {
            return PitstopCardFixStep.Cancelled;
        }

        if (confirmedNotPaid)
        {
            return PitstopCardFixStep.NotCharged;
        }

        _ = canRebuildSale;
        return PitstopCardFixStep.Unknown;
    }

    public static CardFixOpenKind OpenKind(bool approved, bool requiresRecovery, bool cancelled)
    {
        if (approved)
        {
            return CardFixOpenKind.None;
        }

        if (requiresRecovery)
        {
            return CardFixOpenKind.Recovery;
        }

        return cancelled ? CardFixOpenKind.None : CardFixOpenKind.NotCharged;
    }

    public static bool ShouldCloseQuietly(PitstopCardFixStep step) =>
        step == PitstopCardFixStep.Cancelled;

    public static string BannerText(decimal amount) =>
        $"A card still needs a look — {amount.ToString("C2")}";

    public static bool WrittenDownClearsBanner(PitstopCardFixStep step, bool canRebuildSale) =>
        step == PitstopCardFixStep.Paid && !canRebuildSale;

    public static bool ShouldAutoCheck(PitstopCardFixStep step) =>
        step == PitstopCardFixStep.AskCheck;

    public static bool ShouldAutoSave(PitstopCardFixStep step, bool canRebuildSale) =>
        step == PitstopCardFixStep.Paid && canRebuildSale;
}

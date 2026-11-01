namespace NickeltownPOSV4.Services.Pitstop;

public enum PitstopCardFixStep
{
    AskCheck,
    Paid,
    NotCharged,
    Unknown,
    Saved,
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

    public string CheckLabel { get; init; } = "Find out if they paid";

    public string SaveLabel { get; init; } = "Save the sale";

    public string NotChargedLabel { get; init; } = "They weren't charged";

    public string OkLabel { get; init; } = "OK — keep selling";
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
            PitstopCardFixStep.NotCharged => new PitstopCardFixCopy
            {
                Title = "They weren't charged",
                Body = "No money was taken. Take cash, or start the card again as a new sale.",
                AmountText = money,
                ShowOk = true,
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

    public static PitstopCardFixStep AfterSquareCheck(bool approved, bool confirmedNotPaid, bool canRebuildSale)
    {
        if (approved)
        {
            return PitstopCardFixStep.Paid;
        }

        if (confirmedNotPaid)
        {
            return PitstopCardFixStep.NotCharged;
        }

        _ = canRebuildSale;
        return PitstopCardFixStep.Unknown;
    }

    public static string BannerText(decimal amount) =>
        $"A card still needs a look — {amount.ToString("C2")}";

    public static bool WrittenDownClearsBanner(PitstopCardFixStep step, bool canRebuildSale) =>
        step == PitstopCardFixStep.Paid && !canRebuildSale;
}

using NickeltownPOSV4.Services.Pitstop;

namespace NickeltownPOSV4.Services.Tabs;

/// <summary>
/// Plain-English next step after a tab card payment does not finish.
/// Same buttons as Pitstop. No Square IDs, no Admin, no PIN.
/// </summary>
public static class TabCardFixGuide
{
    public static PitstopCardFixCopy Build(
        PitstopCardFixStep step,
        decimal amount,
        bool canRebuildCredit,
        bool canAskReader = true,
        string savedNoun = "credit")
    {
        var money = amount.ToString("C2");
        return step switch
        {
            PitstopCardFixStep.Paid => new PitstopCardFixCopy
            {
                Title = "They paid",
                Body = canRebuildCredit
                    ? "The card went through. Saving the credit. Do not charge them again."
                    : "The card went through, but this till cannot save the credit. Write down the amount. Do not take cash for this same payment.",
                AmountText = money,
                ShowSaveSale = canRebuildCredit,
                ShowOk = !canRebuildCredit,
                SaveLabel = "Save the credit",
                OkLabel = canRebuildCredit ? "OK — keep going" : "I've written it down",
            },
            PitstopCardFixStep.Checking => new PitstopCardFixCopy
            {
                Title = "Checking if they paid",
                Body = "Asking the card reader. This does not charge anyone.",
                AmountText = money,
            },
            PitstopCardFixStep.Saving => new PitstopCardFixCopy
            {
                Title = "Saving the credit",
                Body = "The card went through. Saving it now. Do not charge them again.",
                AmountText = money,
            },
            PitstopCardFixStep.NotCharged => new PitstopCardFixCopy
            {
                Title = "They weren't charged",
                Body = "No money was taken. Try the card again, or take cash.",
                AmountText = money,
                ShowRetryCard = true,
                ShowTakeCash = true,
            },
            PitstopCardFixStep.Saved => new PitstopCardFixCopy
            {
                Title = "Credit saved",
                Body = $"The {savedNoun} is on the tab. Do not charge them again.",
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
                Body = "Do not run the card again. The till will check the reader. That does not charge anyone.",
                AmountText = money,
                ShowCheck = true,
            },
        };
    }
}

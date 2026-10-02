namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>
/// Enablement for the Pitstop retail Pay button.
/// The XAML binds <c>IsEnabled</c> to this result, so cart/lock changes
/// must raise <c>PayButtonEnabled</c> or the button stays dead.
/// </summary>
public static class PitstopPayButtonRules
{
    public static bool CanStart(int cartLineCount, bool isPaymentLocked, bool isSendingSquare) =>
        cartLineCount > 0 && !isPaymentLocked && !isSendingSquare;
}

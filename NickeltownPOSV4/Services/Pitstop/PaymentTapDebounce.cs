namespace NickeltownPOSV4.Services.Pitstop;

internal static class PaymentTapDebounce
{
    public const int DefaultMilliseconds = MoneyActionGuard.DefaultMilliseconds;

    public static bool TryEnter(int milliseconds = DefaultMilliseconds) =>
        MoneyActionGuard.TryEnter(milliseconds);
}

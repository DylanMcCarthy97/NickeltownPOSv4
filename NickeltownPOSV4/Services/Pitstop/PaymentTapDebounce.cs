using System;

namespace NickeltownPOSV4.Services.Pitstop;

internal static class PaymentTapDebounce
{
    private static DateTime _lastTapUtc = DateTime.MinValue;
    public const int DefaultMilliseconds = 600;
    public static bool TryEnter(int milliseconds = DefaultMilliseconds)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTapUtc).TotalMilliseconds < milliseconds) return false;
        _lastTapUtc = now;
        return true;
    }
}

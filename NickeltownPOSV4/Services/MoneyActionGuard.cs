using System;
using System.Threading;

namespace NickeltownPOSV4.Services;

/// <summary>
/// Touchscreen double-tap protection for money actions.
/// WinUI can queue a second Click before <c>CanExecute</c> refreshes; this
/// debounce plus <see cref="MoneyActionLock"/> make the second tap a no-op.
/// Buttons must still disable via <c>IsBusy</c>/<c>CanExecute</c>.
/// </summary>
public static class MoneyActionGuard
{
    public const int DefaultMilliseconds = 600;

    private static long _lastTapTicks;

    public static bool TryEnter(int milliseconds = DefaultMilliseconds)
    {
        var now = DateTime.UtcNow.Ticks;
        var window = TimeSpan.FromMilliseconds(milliseconds).Ticks;
        while (true)
        {
            var last = Interlocked.Read(ref _lastTapTicks);
            if (last != 0 && now - last < window)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _lastTapTicks, now, last) == last)
            {
                return true;
            }
        }
    }
}

/// <summary>
/// Per-screen in-flight lock so the same Pay/Add/Top Up/Complete control
/// cannot start two money writes even if debounce is bypassed.
/// </summary>
public sealed class MoneyActionLock
{
    private int _inFlight;

    public bool IsInFlight => Volatile.Read(ref _inFlight) != 0;

    public bool TryBegin(bool useGlobalDebounce = true)
    {
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
        {
            return false;
        }

        if (useGlobalDebounce && !MoneyActionGuard.TryEnter())
        {
            Interlocked.Exchange(ref _inFlight, 0);
            return false;
        }

        return true;
    }

    public void End() => Interlocked.Exchange(ref _inFlight, 0);
}

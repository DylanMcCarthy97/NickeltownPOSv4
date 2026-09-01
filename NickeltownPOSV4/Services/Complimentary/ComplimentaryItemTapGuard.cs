using System;
using System.Collections.Concurrent;

namespace NickeltownPOSV4.Services.Complimentary;

/// <summary>
/// Per-product double-tap protection for Quick Free Items.
/// Accidental WinUI double-clicks on the same button are ignored.
/// Intentional repeats are allowed after a short window, and other products are never blocked.
/// </summary>
public sealed class ComplimentaryItemTapGuard
{
    public const int DuplicateWindowMilliseconds = 220;

    private readonly ConcurrentDictionary<long, long> _lastTapTicks = new();
    private readonly ConcurrentDictionary<long, byte> _inFlight = new();

    public bool TryBegin(long itemId, int milliseconds = DuplicateWindowMilliseconds)
    {
        if (itemId <= 0)
        {
            return false;
        }

        if (!_inFlight.TryAdd(itemId, 0))
        {
            return false;
        }

        var now = DateTime.UtcNow.Ticks;
        var window = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).Ticks;
        if (_lastTapTicks.TryGetValue(itemId, out var last) && last != 0 && now - last < window)
        {
            _inFlight.TryRemove(itemId, out _);
            return false;
        }

        _lastTapTicks[itemId] = now;
        return true;
    }

    public void End(long itemId) => _inFlight.TryRemove(itemId, out _);

    public bool IsInFlight(long itemId) => _inFlight.ContainsKey(itemId);
}

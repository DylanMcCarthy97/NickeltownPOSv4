using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Converters;

/// <summary>Shared balance-tier to theme brush keys for tab cards.</summary>
internal static class TabBalanceTierBrushes
{
    public static string StripResourceKey(TabCardModel tab) =>
        tab.IsGuest ? "PosTabStripGuestBrush" : StripResourceKey(tab.BalanceTier);

    public static string StripResourceKey(TabBalanceTier tier) =>
        tier switch
        {
            TabBalanceTier.Negative => "PosTabStripOwingBrush",
            TabBalanceTier.Settled => "PosTabStripSettledBrush",
            TabBalanceTier.Low => "PosTabStripLowBrush",
            _ => "PosTabStripCreditBrush",
        };

    public static string BalanceResourceKey(TabBalanceTier tier) =>
        tier switch
        {
            TabBalanceTier.Negative => "PosBalanceNegativeBrush",
            TabBalanceTier.Settled => "PosBalanceSettledBrush",
            TabBalanceTier.Low => "PosBalanceLowBrush",
            _ => "PosBalanceGoodBrush",
        };
}
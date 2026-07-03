using System;
using System.Globalization;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

internal static class MembershipFeeCalculator
{
    public static MembershipFeeType GetApplicableFeeType(DateOnly referenceDate)
    {
        var month = referenceDate.Month;
        return month is >= 7 and <= 12 ? MembershipFeeType.FullYear : MembershipFeeType.HalfYear;
    }

    public static decimal GetApplicableAmount(MembershipSettings settings, DateOnly referenceDate)
    {
        return GetApplicableFeeType(referenceDate) == MembershipFeeType.FullYear
            ? settings.JoiningFeeFull
            : settings.JoiningFeeHalf;
    }

    public static decimal ResolveSelectedAmount(
        MembershipSettings settings,
        MembershipFeeType? feeType,
        decimal? selectedFee)
    {
        if (feeType == MembershipFeeType.Override && selectedFee.HasValue)
        {
            return decimal.Round(Math.Max(0m, selectedFee.Value), 2, MidpointRounding.AwayFromZero);
        }

        if (feeType == MembershipFeeType.FullYear)
        {
            return settings.JoiningFeeFull;
        }

        if (feeType == MembershipFeeType.HalfYear)
        {
            return settings.JoiningFeeHalf;
        }

        var applicable = GetApplicableAmount(settings, DateOnly.FromDateTime(DateTime.Now));
        return applicable;
    }

    public static string FormatMoney(decimal amount) =>
        amount.ToString("C2", CultureInfo.GetCultureInfo("en-AU"));
}

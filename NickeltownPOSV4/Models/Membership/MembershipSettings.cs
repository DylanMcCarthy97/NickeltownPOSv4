using System;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipSettings
{
    public const int SingletonId = 1;

    public int Id { get; init; } = SingletonId;

    public string MembershipYearLabel { get; init; } = "2026/2027";

    public DateOnly MembershipYearStart { get; init; } = new(2026, 7, 1);

    public DateOnly MembershipYearEnd { get; init; } = new(2027, 6, 30);

    public decimal JoiningFeeFull { get; init; } = 65.00m;

    public decimal JoiningFeeHalf { get; init; } = 32.50m;

    public decimal RenewalFee { get; init; }

    public int ReminderDaysBeforeExpiry { get; init; } = 30;

    public string CommitteeEmail { get; init; } = "nickeltown@gmail.com";

    public string ClubName { get; init; } = "Nickeltown Flounderers Inc Auto Club";

    public string ClubAbn { get; init; } = "45 087 371 412";

    public string ClubPoBox { get; init; } = "PO Box 31, Kambalda WA 6442";

    public string ClubPhone { get; init; } = "0410 065 002";

    public string ClubEmail { get; init; } = "nickeltown@gmail.com";

    public string? LogoPath { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

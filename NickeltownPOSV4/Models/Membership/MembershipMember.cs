using System;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipMember
{
    public long Id { get; init; }

    public long? ApplicationId { get; init; }

    public long? PosMemberId { get; init; }

    public string? MemberNumber { get; init; }

    public string? Surname { get; init; }

    public string? GivenNames { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Mobile { get; init; }

    public string? Address { get; init; }

    public string? PostCode { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public string? MembershipYearLabel { get; init; }

    public DateOnly? MembershipStartsAt { get; init; }

    public DateOnly? MembershipExpiresAt { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTimeOffset? ReceiptIssuedAt { get; init; }

    public bool AddedToDistributionList { get; init; }

    public bool CardIssued { get; init; }

    public bool WelcomeBagIssued { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

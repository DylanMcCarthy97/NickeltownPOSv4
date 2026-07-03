using System;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipMemberListItem
{
    public long Id { get; init; }

    public string? MemberNumber { get; init; }

    public string MemberName { get; init; } = string.Empty;

    public ApplicationStatus ApplicationStatus { get; init; }

    public DateOnly? MembershipExpiresAt { get; init; }

    public bool IsActive { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }
}

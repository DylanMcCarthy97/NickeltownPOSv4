using System;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipApplicationListItem
{
    public long Id { get; init; }

    public string? ApplicationNumber { get; init; }

    public string ApplicantName { get; init; } = string.Empty;

    public ApplicationSource Source { get; init; }

    public ApplicationStatus Status { get; init; }

    public MembershipPaymentStatus PaymentStatus { get; init; }

    public bool HasLinkedMember { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? PrimaryVehicleRegistration { get; init; }
}

using System;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipApplicationTimelineEvent
{
    public long Id { get; init; }

    public long ApplicationId { get; init; }

    public MembershipTimelineEventType EventType { get; init; }

    public string User { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; }

    public string Description { get; init; } = string.Empty;
}

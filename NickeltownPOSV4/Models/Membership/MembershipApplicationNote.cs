using System;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipApplicationNote
{
    public long Id { get; init; }

    public long ApplicationId { get; init; }

    public string Author { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public string Text { get; init; } = string.Empty;
}

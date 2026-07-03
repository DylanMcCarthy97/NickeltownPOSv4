using System;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipFormContentSection
{
    public long Id { get; init; }

    public string SectionKey { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string Body { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

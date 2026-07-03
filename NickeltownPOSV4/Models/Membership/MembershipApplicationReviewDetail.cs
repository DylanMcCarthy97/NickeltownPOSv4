using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipApplicationReviewDetail
{
    public MembershipApplication Application { get; init; } = null!;

    public IReadOnlyList<MembershipApplicationVehicle> Vehicles { get; init; } = [];

    public IReadOnlyList<MembershipApplicationNote> Notes { get; init; } = [];

    public IReadOnlyList<MembershipApplicationTimelineEvent> TimelineEvents { get; init; } = [];
}

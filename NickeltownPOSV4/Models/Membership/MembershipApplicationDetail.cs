using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipApplicationDetail
{
    public MembershipApplication Application { get; init; } = null!;

    public IReadOnlyList<MembershipApplicationVehicle> Vehicles { get; init; } = [];
}

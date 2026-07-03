namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipApplicationVehicle
{
    public long Id { get; init; }

    public long ApplicationId { get; init; }

    public string? MakeModel { get; init; }

    public string? Year { get; init; }

    public string? BodyType { get; init; }

    public string? Engine { get; init; }

    public string? RegistrationNumber { get; init; }

    public string? ClubRego { get; init; }

    public string? Colour { get; init; }

    public string? Modifications { get; init; }

    public int SortOrder { get; init; }
}

namespace NickeltownPOSV4.Models;

public sealed class TabMutationResult
{
    public bool Ok { get; private init; }

    public string? ErrorMessage { get; private init; }

    public string? CreatedLegacyId { get; private init; }

    public static TabMutationResult Success(string? createdLegacyId = null) =>
        new() { Ok = true, CreatedLegacyId = createdLegacyId };

    public static TabMutationResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

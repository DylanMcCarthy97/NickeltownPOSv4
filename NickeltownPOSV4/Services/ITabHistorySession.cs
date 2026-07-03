namespace NickeltownPOSV4.Services;

public interface ITabHistorySession
{
    string? TabLegacyId { get; set; }

    string? TabDisplayName { get; set; }

    void Clear();
}

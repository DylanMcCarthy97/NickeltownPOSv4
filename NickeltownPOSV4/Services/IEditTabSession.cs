namespace NickeltownPOSV4.Services;

public interface IEditTabSession
{
    string? TabLegacyId { get; set; }

    void Clear();
}

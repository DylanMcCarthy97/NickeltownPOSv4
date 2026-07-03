namespace NickeltownPOSV4.Services;

/// <summary>Signed-in staff session (replaces legacy main-form bartender context).</summary>
public interface IUserSessionService : System.ComponentModel.INotifyPropertyChanged
{
    bool IsSignedIn { get; }

    long? ActiveStaffId { get; }

    string? ActiveStaffLegacyId { get; }

    string? DisplayName { get; }

    string? Role { get; }

    bool IsAdmin { get; }

    bool IsTreasurer { get; }

    /// <summary>True when the user can perform Admin/Treasurer-only actions (archive, void, recovery, etc.).</summary>
    bool IsManager { get; }

    /// <summary>Admin can view the Admin route.</summary>
    bool CanAccessAdmin { get; }

    /// <summary>Admin/Treasurer can view the Reports route.</summary>
    bool CanAccessReports { get; }

    /// <summary>Treasurer-only routes (finance/audit). Admins also get access to keep the clubroom workflow simple.</summary>
    bool CanAccessTreasurer { get; }

    void SetSignedIn(long staffPk, string? legacyId, string displayName, string? role);

    void Clear();
}

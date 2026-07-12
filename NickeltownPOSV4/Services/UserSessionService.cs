using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NickeltownPOSV4.Services;

public sealed class UserSessionService : IUserSessionService
{
    private bool _isSignedIn;

    private long? _activeStaffId;

    private string? _activeStaffLegacyId;

    private string? _displayName;

    private string? _role;

    private bool _isDeveloper;

    public bool IsSignedIn
    {
        get => _isSignedIn;
        private set => SetField(ref _isSignedIn, value);
    }

    public long? ActiveStaffId
    {
        get => _activeStaffId;
        private set => SetField(ref _activeStaffId, value);
    }

    public string? ActiveStaffLegacyId
    {
        get => _activeStaffLegacyId;
        private set => SetField(ref _activeStaffLegacyId, value);
    }

    public string? DisplayName
    {
        get => _displayName;
        private set => SetField(ref _displayName, value);
    }

    public string? Role
    {
        get => _role;
        private set
        {
            if (!SetField(ref _role, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsTreasurer));
            OnPropertyChanged(nameof(IsManager));
            OnPropertyChanged(nameof(CanAccessAdmin));
            OnPropertyChanged(nameof(CanAccessReports));
            OnPropertyChanged(nameof(CanAccessTreasurer));
        }
    }

    public bool IsAdmin =>
        string.Equals((Role ?? string.Empty).Trim(), "admin", StringComparison.OrdinalIgnoreCase);

    public bool IsTreasurer =>
        string.Equals((Role ?? string.Empty).Trim(), "treasurer", StringComparison.OrdinalIgnoreCase);

    public bool IsManager => IsAdmin || IsTreasurer;

    public bool CanAccessAdmin => IsManager;

    public bool CanAccessReports => IsManager;

    /// <summary>Treasurer + Admin can open Treasurer-scoped screens (Previous Pitstops finance details, Square Recovery, audit log).</summary>
    public bool CanAccessTreasurer => IsManager;

    public bool IsDeveloper => _isDeveloper;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetSignedIn(long staffPk, string? legacyId, string displayName, string? role, bool isDeveloper = false)
    {
        var normalizedRole = NormalizeRole(role);
        ActiveStaffId = staffPk;
        ActiveStaffLegacyId = legacyId;
        DisplayName = displayName;
        Role = normalizedRole;
        SetField(ref _isDeveloper, isDeveloper);
        OnPropertyChanged(nameof(IsDeveloper));
        IsSignedIn = true;
    }

    public void Clear()
    {
        IsSignedIn = false;
        ActiveStaffId = null;
        ActiveStaffLegacyId = null;
        DisplayName = null;
        Role = null;
        SetField(ref _isDeveloper, false);
        OnPropertyChanged(nameof(IsDeveloper));
    }

    private static string? NormalizeRole(string? role)
    {
        var r = (role ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(r))
        {
            return null;
        }

        if (string.Equals(r, "treasurer", StringComparison.OrdinalIgnoreCase))
        {
            return "treasurer";
        }

        if (string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return "admin";
        }

        return "staff";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class UserManagementViewModel : SettingsSubViewModelBase
{
    private readonly IStaffAdminService _service;
    private readonly IInputOverlayService _inputOverlay;

    private readonly System.Collections.Generic.List<StaffRowViewModel> _allStaff = new();

    private bool _isEditorOpen;
    private bool _editorIsAdd;
    private bool _editorIsResetPin;
    private long? _editorTargetId;
    private string _editorTitle = string.Empty;
    private string _editorDisplayName = string.Empty;
    private string _editorRole = "Staff";
    private bool _editorIsActive = true;
    private bool _editorIsDeveloper;
    private string _editorPin = string.Empty;

    public UserManagementViewModel(
        INavigationService navigation,
        IStaffAdminService service,
        IInputOverlayService inputOverlay)
        : base(navigation)
    {
        _service = service;
        _inputOverlay = inputOverlay;

        Staff = new PagedCollection<StaffRowViewModel>(pageSize: 6);

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenAddCommand = new RelayCommand(BeginAdd);
        EditDisplayNameCommand = new AsyncRelayCommand(EditDisplayNameAsync);
        EditPinCommand = new AsyncRelayCommand(EditPinAsync);
        ToggleRoleCommand = new RelayCommand(ToggleRole);
        SaveEditorCommand = new AsyncRelayCommand(SaveEditorAsync, () => !IsBusy);
        CancelEditorCommand = new RelayCommand(CloseEditor);
    }

    public PagedCollection<StaffRowViewModel> Staff { get; }

    public StaffRowViewModel? FindStaffById(long id) =>
        _allStaff.FirstOrDefault(s => s.Id == id);

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand OpenAddCommand { get; }

    public IAsyncRelayCommand EditDisplayNameCommand { get; }

    public IAsyncRelayCommand EditPinCommand { get; }

    public IRelayCommand ToggleRoleCommand { get; }

    public IAsyncRelayCommand SaveEditorCommand { get; }

    public IRelayCommand CancelEditorCommand { get; }

    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        private set
        {
            if (SetProperty(ref _isEditorOpen, value))
            {
                OnPropertyChanged(nameof(IsListVisible));
            }
        }
    }

    public bool IsListVisible => !IsEditorOpen;

    public bool EditorIsAdd
    {
        get => _editorIsAdd;
        private set => SetProperty(ref _editorIsAdd, value);
    }

    public bool EditorIsResetPin
    {
        get => _editorIsResetPin;
        private set
        {
            if (SetProperty(ref _editorIsResetPin, value))
            {
                OnPropertyChanged(nameof(EditorShowsNameAndRole));
                OnPropertyChanged(nameof(EditorShowsPin));
                OnPropertyChanged(nameof(EditorShowsActiveToggle));
            }
        }
    }

    public bool EditorShowsNameAndRole => !EditorIsResetPin;

    public bool EditorShowsPin => EditorIsResetPin || EditorIsAdd;

    public bool EditorShowsActiveToggle => !EditorIsAdd && !EditorIsResetPin;

    public string EditorTitle
    {
        get => _editorTitle;
        private set => SetProperty(ref _editorTitle, value);
    }

    public string EditorDisplayName
    {
        get => _editorDisplayName;
        private set
        {
            if (SetProperty(ref _editorDisplayName, value))
            {
                OnPropertyChanged(nameof(EditorDisplayNameSummary));
            }
        }
    }

    public string EditorDisplayNameSummary =>
        string.IsNullOrWhiteSpace(EditorDisplayName) ? "Tap to enter display name" : EditorDisplayName;

    public string EditorRole
    {
        get => _editorRole;
        private set
        {
            if (SetProperty(ref _editorRole, value))
            {
                OnPropertyChanged(nameof(EditorRoleSummary));
            }
        }
    }

    public string EditorRoleSummary
    {
        get
        {
            // if (string.Equals(EditorRole, "Treasurer", StringComparison.OrdinalIgnoreCase))
            // {
            //     return "Treasurer";
            // }

            return string.Equals(EditorRole, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Staff";
        }
    }

    public bool EditorIsActive
    {
        get => _editorIsActive;
        set => SetProperty(ref _editorIsActive, value);
    }

    public bool EditorIsDeveloper
    {
        get => _editorIsDeveloper;
        set => SetProperty(ref _editorIsDeveloper, value);
    }

    public string EditorPin
    {
        get => _editorPin;
        private set
        {
            if (SetProperty(ref _editorPin, value))
            {
                OnPropertyChanged(nameof(EditorPinSummary));
                SaveEditorCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string EditorPinSummary =>
        string.IsNullOrEmpty(EditorPin) ? "Tap to set 4-digit PIN" : new string('•', EditorPin.Length);

    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            SetStatus("Loading staff...");
            var rows = await _service.ListAsync().ConfigureAwait(true);
            _allStaff.Clear();
            foreach (var r in rows)
            {
                _allStaff.Add(new StaffRowViewModel(r));
            }

            Staff.Replace(_allStaff);
            SetStatus($"{Staff.TotalCount} staff loaded.");
        }
        catch (Exception ex)
        {
            SetStatus($"Load failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BeginAdd()
    {
        _editorTargetId = null;
        EditorIsAdd = true;
        EditorIsResetPin = false;
        EditorTitle = "Add user";
        EditorDisplayName = string.Empty;
        EditorRole = "Staff";
        EditorIsActive = true;
        EditorIsDeveloper = false;
        EditorPin = string.Empty;
        SetStatus(string.Empty);
        IsEditorOpen = true;
    }

    public void BeginEdit(StaffRowViewModel row)
    {
        _editorTargetId = row.Id;
        EditorIsAdd = false;
        EditorIsResetPin = false;
        EditorTitle = $"Edit {row.DisplayName}";
        EditorDisplayName = row.DisplayName;
        EditorRole = row.Role;
        EditorIsActive = row.IsActive;
        EditorIsDeveloper = row.IsDeveloper;
        EditorPin = string.Empty;
        SetStatus(string.Empty);
        IsEditorOpen = true;
    }

    public void BeginResetPin(StaffRowViewModel row)
    {
        _editorTargetId = row.Id;
        EditorIsAdd = false;
        EditorIsResetPin = true;
        EditorTitle = $"Reset PIN — {row.DisplayName}";
        EditorDisplayName = row.DisplayName;
        EditorRole = row.Role;
        EditorIsActive = row.IsActive;
        EditorIsDeveloper = row.IsDeveloper;
        EditorPin = string.Empty;
        SetStatus(string.Empty);
        IsEditorOpen = true;
    }

    public void CloseEditor()
    {
        IsEditorOpen = false;
        _editorTargetId = null;
        EditorIsAdd = false;
        EditorIsResetPin = false;
        EditorPin = string.Empty;
    }

    private async Task EditDisplayNameAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(EditorDisplayName ?? string.Empty, "Display name").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        EditorDisplayName = result.Trim();
    }

    private async Task EditPinAsync()
    {
        var title = EditorIsResetPin
            ? $"New PIN for {EditorDisplayName}"
            : "Set 4-digit PIN";
        var result = await _inputOverlay.ShowPinNumpadAsync(title, digitCount: 4, maskDisplay: false).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        EditorPin = result;
    }

    private void ToggleRole()
    {
        // Cycle Staff ↔ Admin (Treasurer role disabled).
        EditorRole = EditorRole?.Trim().ToLowerInvariant() switch
        {
            "staff" => "Admin",
            "admin" => "Staff",
            // "treasurer" => "Staff",
            _ => "Staff",
        };
    }

    private async Task SaveEditorAsync()
    {
        if (EditorIsResetPin)
        {
            if (_editorTargetId is null)
            {
                SetStatus("Nothing to reset.");
                return;
            }

            if (EditorPin.Length != 4)
            {
                SetStatus("PIN must be 4 digits.");
                return;
            }

            await ResetPinAsync(_editorTargetId.Value, EditorPin).ConfigureAwait(true);
            if (StatusMessage.StartsWith("PIN", StringComparison.OrdinalIgnoreCase))
            {
                CloseEditor();
            }

            return;
        }

        if (EditorIsAdd)
        {
            if (string.IsNullOrWhiteSpace(EditorDisplayName))
            {
                SetStatus("Display name is required.");
                return;
            }

            if (EditorPin.Length != 4)
            {
                SetStatus("PIN must be 4 digits.");
                return;
            }

            var id = await CreateAsync(EditorDisplayName, EditorRole, EditorPin).ConfigureAwait(true);
            if (id is not null)
            {
                CloseEditor();
            }

            return;
        }

        // Edit existing.
        if (_editorTargetId is null)
        {
            SetStatus("Nothing to update.");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditorDisplayName))
        {
            SetStatus("Display name is required.");
            return;
        }

        var ok = await UpdateAsync(_editorTargetId.Value, EditorDisplayName, EditorRole, EditorIsActive, EditorIsDeveloper).ConfigureAwait(true);
        if (ok)
        {
            CloseEditor();
        }
    }

    public async Task<long?> CreateAsync(string displayName, string role, string pin4)
    {
        try
        {
            IsBusy = true;
            SaveEditorCommand.NotifyCanExecuteChanged();
            var id = await _service.CreateAsync(displayName, role, pin4).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatus($"Added {displayName}.");
            return id;
        }
        catch (Exception ex)
        {
            SetStatus($"Add failed: {ex.Message}");
            return null;
        }
        finally
        {
            IsBusy = false;
            SaveEditorCommand.NotifyCanExecuteChanged();
        }
    }

    public async Task<bool> UpdateAsync(long id, string displayName, string role, bool isActive, bool isDeveloper)
    {
        try
        {
            IsBusy = true;
            SaveEditorCommand.NotifyCanExecuteChanged();
            await _service.UpdateAsync(id, displayName, role, isActive, isDeveloper).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatus($"Updated {displayName}.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Update failed: {ex.Message}");
            return false;
        }
        finally
        {
            IsBusy = false;
            SaveEditorCommand.NotifyCanExecuteChanged();
        }
    }

    public async Task<bool> ResetPinAsync(long id, string newPin4)
    {
        try
        {
            IsBusy = true;
            SaveEditorCommand.NotifyCanExecuteChanged();
            await _service.ResetPinAsync(id, newPin4).ConfigureAwait(true);
            SetStatus("PIN reset.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"PIN reset failed: {ex.Message}");
            return false;
        }
        finally
        {
            IsBusy = false;
            SaveEditorCommand.NotifyCanExecuteChanged();
        }
    }

    public async Task<bool> DeactivateAsync(long id, string displayName)
    {
        try
        {
            IsBusy = true;
            await _service.DeleteAsync(id).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatus($"Deactivated {displayName}.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Deactivate failed: {ex.Message}");
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class StaffRowViewModel : ObservableViewModel
{
    public StaffRowViewModel(StaffAdminRow row)
    {
        Id = row.Id;
        DisplayName = row.DisplayName;
        Role = row.Role;
        IsActive = row.IsActive;
        IsDeveloper = row.IsDeveloper;
        LegacyId = row.LegacyId;
    }

    public long Id { get; }

    public string? LegacyId { get; }

    public string DisplayName { get; }

    public string Role { get; }

    public bool IsActive { get; }

    public bool IsDeveloper { get; }

    public string SubtitleText
    {
        get
        {
            var parts = new System.Collections.Generic.List<string> { Role };
            if (IsDeveloper)
            {
                parts.Add("Developer");
            }

            var text = string.Join(" · ", parts);
            return IsActive ? text : $"{text} (inactive)";
        }
    }
}

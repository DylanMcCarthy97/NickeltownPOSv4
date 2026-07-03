using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Membership;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipApplicationVehicleEntryViewModel : ObservableViewModel
{
    private readonly IInputOverlayService _inputOverlay;
    private readonly Action<MembershipApplicationVehicleEntryViewModel> _requestRemove;

    private string _makeModel = string.Empty;
    private string _year = string.Empty;
    private string _bodyType = string.Empty;
    private string _engine = string.Empty;
    private string _registrationNumber = string.Empty;
    private string _clubRego = "No";
    private string _colour = string.Empty;
    private string _modifications = string.Empty;
    private string _title = "Vehicle";

    public MembershipApplicationVehicleEntryViewModel(
        IInputOverlayService inputOverlay,
        Action<MembershipApplicationVehicleEntryViewModel> requestRemove,
        int index)
    {
        _inputOverlay = inputOverlay;
        _requestRemove = requestRemove;
        UpdateTitle(index);

        EditMakeModelCommand = new AsyncRelayCommand(() => EditTextAsync(_makeModel, "Make/Model", v => MakeModel = v));
        EditYearCommand = new AsyncRelayCommand(() => EditTextAsync(_year, "Year", v => Year = v));
        EditBodyTypeCommand = new AsyncRelayCommand(() => EditTextAsync(_bodyType, "Body type", v => BodyType = v));
        EditEngineCommand = new AsyncRelayCommand(() => EditTextAsync(_engine, "Engine", v => Engine = v));
        EditRegistrationNumberCommand = new AsyncRelayCommand(() => EditTextAsync(_registrationNumber, "Registration number", v => RegistrationNumber = v));
        ToggleClubRegoCommand = new RelayCommand(ToggleClubRego);
        EditColourCommand = new AsyncRelayCommand(() => EditTextAsync(_colour, "Colour", v => Colour = v));
        EditModificationsCommand = new AsyncRelayCommand(() => EditTextAsync(_modifications, "Modifications", v => Modifications = v));
        RemoveCommand = new RelayCommand(() => _requestRemove(this));
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public void UpdateTitle(int index) =>
        Title = index <= 0 ? "Primary vehicle" : $"Additional vehicle {index}";

    public string MakeModel
    {
        get => _makeModel;
        set
        {
            if (SetProperty(ref _makeModel, value))
            {
                OnPropertyChanged(nameof(MakeModelSummary));
            }
        }
    }

    public string Year
    {
        get => _year;
        set
        {
            if (SetProperty(ref _year, value))
            {
                OnPropertyChanged(nameof(YearSummary));
            }
        }
    }

    public string BodyType
    {
        get => _bodyType;
        set
        {
            if (SetProperty(ref _bodyType, value))
            {
                OnPropertyChanged(nameof(BodyTypeSummary));
            }
        }
    }

    public string Engine
    {
        get => _engine;
        set
        {
            if (SetProperty(ref _engine, value))
            {
                OnPropertyChanged(nameof(EngineSummary));
            }
        }
    }

    public string RegistrationNumber
    {
        get => _registrationNumber;
        set
        {
            if (SetProperty(ref _registrationNumber, value))
            {
                OnPropertyChanged(nameof(RegistrationNumberSummary));
            }
        }
    }

    public string ClubRego
    {
        get => _clubRego;
        set
        {
            if (SetProperty(ref _clubRego, value))
            {
                OnPropertyChanged(nameof(ClubRegoSummary));
            }
        }
    }

    public string Colour
    {
        get => _colour;
        set
        {
            if (SetProperty(ref _colour, value))
            {
                OnPropertyChanged(nameof(ColourSummary));
            }
        }
    }

    public string Modifications
    {
        get => _modifications;
        set
        {
            if (SetProperty(ref _modifications, value))
            {
                OnPropertyChanged(nameof(ModificationsSummary));
            }
        }
    }

    public string MakeModelSummary => DisplayOrPlaceholder(MakeModel);
    public string YearSummary => DisplayOrPlaceholder(Year);
    public string BodyTypeSummary => DisplayOrPlaceholder(BodyType);
    public string EngineSummary => DisplayOrPlaceholder(Engine);
    public string RegistrationNumberSummary => DisplayOrPlaceholder(RegistrationNumber);
    public string ClubRegoSummary => ClubRego;
    public string ColourSummary => DisplayOrPlaceholder(Colour);
    public string ModificationsSummary => DisplayOrPlaceholder(Modifications);

    public IAsyncRelayCommand EditMakeModelCommand { get; }
    public IAsyncRelayCommand EditYearCommand { get; }
    public IAsyncRelayCommand EditBodyTypeCommand { get; }
    public IAsyncRelayCommand EditEngineCommand { get; }
    public IAsyncRelayCommand EditRegistrationNumberCommand { get; }
    public IRelayCommand ToggleClubRegoCommand { get; }
    public IAsyncRelayCommand EditColourCommand { get; }
    public IAsyncRelayCommand EditModificationsCommand { get; }
    public IRelayCommand RemoveCommand { get; }

    public MembershipApplicationVehicle ToModel(long applicationId, int sortOrder) =>
        new()
        {
            ApplicationId = applicationId,
            MakeModel = NullIfEmpty(MakeModel),
            Year = NullIfEmpty(Year),
            BodyType = NullIfEmpty(BodyType),
            Engine = NullIfEmpty(Engine),
            RegistrationNumber = NullIfEmpty(RegistrationNumber),
            ClubRego = NullIfEmpty(ClubRego),
            Colour = NullIfEmpty(Colour),
            Modifications = NullIfEmpty(Modifications),
            SortOrder = sortOrder,
        };

    public void Apply(MembershipApplicationVehicle vehicle)
    {
        MakeModel = vehicle.MakeModel ?? string.Empty;
        Year = vehicle.Year ?? string.Empty;
        BodyType = vehicle.BodyType ?? string.Empty;
        Engine = vehicle.Engine ?? string.Empty;
        RegistrationNumber = vehicle.RegistrationNumber ?? string.Empty;
        ClubRego = string.IsNullOrWhiteSpace(vehicle.ClubRego) ? "No" : vehicle.ClubRego.Trim();
        Colour = vehicle.Colour ?? string.Empty;
        Modifications = vehicle.Modifications ?? string.Empty;
    }

    private void ToggleClubRego() => ClubRego = string.Equals(ClubRego, "Yes", StringComparison.OrdinalIgnoreCase) ? "No" : "Yes";

    private async Task EditTextAsync(string current, string title, Action<string> apply)
    {
        var result = await _inputOverlay.ShowKeyboardAsync(current, title).ConfigureAwait(true);
        if (result is not null)
        {
            apply(result.Trim());
        }
    }

    private static string DisplayOrPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Tap to enter" : value;

    private static string? NullIfEmpty(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
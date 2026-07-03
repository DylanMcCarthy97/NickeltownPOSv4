using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Membership;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Membership;
using NickeltownPOSV4.Views.Membership;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipApplicationsViewModel : MembershipSubViewModelBase
{
    private readonly IMembershipApplicationService _applications;

    private bool _hasItems;

    public MembershipApplicationsViewModel(INavigationService navigation, IMembershipApplicationService applications)
        : base(navigation)
    {
        _applications = applications;
        Items = new ObservableCollection<MembershipApplicationRow>();
        LoadCommand = new AsyncRelayCommand(() => LoadAsync());
        NewPaperApplicationCommand = new RelayCommand(NewPaperApplication);
        ViewCommand = new RelayCommand<MembershipApplicationRow>(ViewApplication, row => row is not null);
        EditCommand = new RelayCommand<MembershipApplicationRow>(EditApplication, CanEditApplication);
        DeleteCommand = new AsyncRelayCommand<MembershipApplicationRow>(DeleteApplicationAsync, CanDeleteApplication);
    }

    public ObservableCollection<MembershipApplicationRow> Items { get; }

    public bool HasItems
    {
        get => _hasItems;
        private set => SetProperty(ref _hasItems, value);
    }

    public IAsyncRelayCommand LoadCommand { get; }

    public IRelayCommand NewPaperApplicationCommand { get; }

    public IRelayCommand<MembershipApplicationRow> ViewCommand { get; }

    public IRelayCommand<MembershipApplicationRow> EditCommand { get; }

    public IAsyncRelayCommand<MembershipApplicationRow> DeleteCommand { get; }

    public async Task LoadAsync(string? successMessage = null)
    {
        try
        {
            IsBusy = true;
            Items.Clear();
            var rows = await _applications.ListAsync().ConfigureAwait(true);
            foreach (var row in rows)
            {
                Items.Add(MembershipApplicationRow.From(row));
            }

            HasItems = rows.Count > 0;
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                SetStatus(successMessage);
            }
            else
            {
                SetStatus(rows.Count == 0
                    ? "No applications yet. Tap New Paper Application to enter a paper form."
                    : $"{rows.Count} application(s) loaded.");
            }
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

    private void NewPaperApplication() => Navigate(typeof(MembershipPaperApplicationPage), 0L);

    private void ViewApplication(MembershipApplicationRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.Status == ApplicationStatus.Draft)
        {
            Navigate(typeof(MembershipPaperApplicationPage), row.Id);
        }
        else
        {
            Navigate(typeof(MembershipApplicationReviewPage), row.Id);
        }
    }

    private void EditApplication(MembershipApplicationRow? row)
    {
        if (row is null)
        {
            return;
        }

        Navigate(typeof(MembershipPaperApplicationPage), row.Id);
    }

    private bool CanEditApplication(MembershipApplicationRow? row) =>
        row is not null && (row.Status == ApplicationStatus.Draft || row.Status == ApplicationStatus.PendingReview);

    private async Task DeleteApplicationAsync(MembershipApplicationRow? row)
    {
        if (row is null || row.Status != ApplicationStatus.Draft)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _applications.DeleteApplicationAsync(row.Id).ConfigureAwait(true);
            await LoadAsync("Draft deleted.").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatus($"Delete failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool CanDeleteApplication(MembershipApplicationRow? row) =>
        row is not null && row.Status == ApplicationStatus.Draft;
}

public sealed class MembershipApplicationRow
{
    public long Id { get; init; }

    public string ApplicationNumber { get; init; } = string.Empty;

    public string ApplicantName { get; init; } = string.Empty;

    public string SourceText { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public ApplicationStatus Status { get; init; }

    public string SubmittedText { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string VehicleRegistration { get; init; } = string.Empty;

    public bool CanDelete => Status == ApplicationStatus.Draft;

    public bool CanEdit => Status == ApplicationStatus.Draft || Status == ApplicationStatus.PendingReview;

    public static MembershipApplicationRow From(MembershipApplicationListItem item)
    {
        var submitted = item.Status == ApplicationStatus.Draft
            ? item.CreatedAt.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture)
            : item.SubmittedAt.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);

        return new MembershipApplicationRow
        {
            Id = item.Id,
            ApplicationNumber = item.ApplicationNumber ?? $"#{item.Id}",
            ApplicantName = item.ApplicantName,
            SourceText = MembershipStatusDisplay.FormatSource(item.Source),
            StatusText = MembershipStatusDisplay.Format(item.Status),
            Status = item.Status,
            SubmittedText = submitted,
            Phone = item.Phone ?? "—",
            Email = item.Email ?? "—",
            VehicleRegistration = item.PrimaryVehicleRegistration ?? "—",
        };
    }
}

using System;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Membership;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class TouchDatePickerOverlayViewModel : ObservableViewModel
{
    private readonly Action<DatePickerOverlayResult> _finish;
    private readonly int _minYear;
    private readonly int _maxYear;
    private int _day;
    private int _month;
    private int _year;

    public TouchDatePickerOverlayViewModel(
        DateOnly? initialValue,
        string title,
        Action<DatePickerOverlayResult> finish,
        int minYear,
        int maxYear)
    {
        _finish = finish;
        _minYear = minYear;
        _maxYear = maxYear;
        Title = string.IsNullOrWhiteSpace(title) ? "Select date" : title.Trim();

        var seed = initialValue ?? DateOnly.FromDateTime(DateTime.Today);
        _day = seed.Day;
        _month = seed.Month;
        _year = Math.Clamp(seed.Year, _minYear, _maxYear);
        ClampDayToMonth();

        IncrementDayCommand = new RelayCommand(() => AdjustDay(1));
        DecrementDayCommand = new RelayCommand(() => AdjustDay(-1));
        IncrementMonthCommand = new RelayCommand(() => AdjustMonth(1));
        DecrementMonthCommand = new RelayCommand(() => AdjustMonth(-1));
        IncrementYearCommand = new RelayCommand(() => AdjustYear(1));
        DecrementYearCommand = new RelayCommand(() => AdjustYear(-1));
        TodayCommand = new RelayCommand(SetToday);
        ClearCommand = new RelayCommand(() => _finish(new DatePickerOverlayResult(Cancelled: false, Value: null)));
        DoneCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => _finish(new DatePickerOverlayResult(Cancelled: true, Value: null)));
    }

    public string Title { get; }

    public int Day
    {
        get => _day;
        private set
        {
            if (SetProperty(ref _day, value))
            {
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }

    public string MonthLabel => CultureInfo.GetCultureInfo("en-AU").DateTimeFormat.GetAbbreviatedMonthName(_month);

    public int Year
    {
        get => _year;
        private set
        {
            if (SetProperty(ref _year, value))
            {
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }

    public string PreviewText => MembershipDateFormats.FormatAustralianDate(new DateOnly(_year, _month, _day));

    public IRelayCommand IncrementDayCommand { get; }

    public IRelayCommand DecrementDayCommand { get; }

    public IRelayCommand IncrementMonthCommand { get; }

    public IRelayCommand DecrementMonthCommand { get; }

    public IRelayCommand IncrementYearCommand { get; }

    public IRelayCommand DecrementYearCommand { get; }

    public IRelayCommand TodayCommand { get; }

    public IRelayCommand ClearCommand { get; }

    public IRelayCommand DoneCommand { get; }

    public IRelayCommand CancelCommand { get; }

    private void AdjustDay(int delta)
    {
        var daysInMonth = DateTime.DaysInMonth(_year, _month);
        var next = _day + delta;
        if (next < 1)
        {
            next = daysInMonth;
        }
        else if (next > daysInMonth)
        {
            next = 1;
        }

        Day = next;
    }

    private void AdjustMonth(int delta)
    {
        var next = _month + delta;
        if (next < 1)
        {
            next = 12;
        }
        else if (next > 12)
        {
            next = 1;
        }

        _month = next;
        OnPropertyChanged(nameof(MonthLabel));
        ClampDayToMonth();
        OnPropertyChanged(nameof(PreviewText));
    }

    private void AdjustYear(int delta)
    {
        var next = Math.Clamp(_year + delta, _minYear, _maxYear);
        if (next == _year)
        {
            return;
        }

        Year = next;
        ClampDayToMonth();
    }

    private void SetToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        _month = today.Month;
        OnPropertyChanged(nameof(MonthLabel));
        Year = Math.Clamp(today.Year, _minYear, _maxYear);
        Day = Math.Min(today.Day, DateTime.DaysInMonth(_year, _month));
    }

    private void ClampDayToMonth()
    {
        var maxDay = DateTime.DaysInMonth(_year, _month);
        if (_day > maxDay)
        {
            Day = maxDay;
        }
    }

    private void Confirm() => _finish(new DatePickerOverlayResult(Cancelled: false, Value: new DateOnly(_year, _month, _day)));
}

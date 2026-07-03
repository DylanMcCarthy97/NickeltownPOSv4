using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;

namespace NickeltownPOSV4.ViewModels;

/// <summary>
/// Lightweight paged view over a source list. Exposes the current page's items as an <see cref="ObservableCollection{T}"/>,
/// plus next/previous commands, and a "1 of N" indicator. Designed for kiosk layouts where we don't want any scrolling.
/// </summary>
public sealed class PagedCollection<T> : ObservableViewModel
{
    private List<T> _source = new();

    private int _pageSize;

    private int _pageIndex;

    public PagedCollection(int pageSize)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be positive.");
        }

        _pageSize = pageSize;

        NextPageCommand = new RelayCommand(NextPage, () => CanGoNext);
        PreviousPageCommand = new RelayCommand(PreviousPage, () => CanGoPrevious);
    }

    public ObservableCollection<T> CurrentPageItems { get; } = new();

    public IRelayCommand NextPageCommand { get; }

    public IRelayCommand PreviousPageCommand { get; }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (value <= 0 || value == _pageSize)
            {
                return;
            }

            _pageSize = value;
            ResetToFirstPage();
        }
    }

    public int PageIndex
    {
        get => _pageIndex;
        private set
        {
            if (SetProperty(ref _pageIndex, value))
            {
                Refresh();
            }
        }
    }

    public int PageCount =>
        _source.Count == 0 ? 1 : (int)Math.Ceiling(_source.Count / (double)_pageSize);

    public int TotalCount => _source.Count;

    public bool CanGoNext => PageIndex + 1 < PageCount;

    public bool CanGoPrevious => PageIndex > 0;

    public bool IsEmpty => _source.Count == 0;

    public bool HasMoreThanOnePage => PageCount > 1;

    public string PageIndicator => $"Page {PageIndex + 1} of {PageCount}";

    public string RangeIndicator
    {
        get
        {
            if (_source.Count == 0)
            {
                return "0 of 0";
            }

            var start = PageIndex * _pageSize + 1;
            var end = Math.Min((PageIndex + 1) * _pageSize, _source.Count);
            return $"{start}–{end} of {_source.Count}";
        }
    }

    public void Replace(IEnumerable<T> items)
    {
        _source = items?.ToList() ?? new List<T>();
        ResetToFirstPage();
    }

    public void Clear()
    {
        _source = new List<T>();
        ResetToFirstPage();
    }

    public void NextPage()
    {
        if (CanGoNext)
        {
            PageIndex++;
        }
    }

    public void PreviousPage()
    {
        if (CanGoPrevious)
        {
            PageIndex--;
        }
    }

    private void ResetToFirstPage()
    {
        if (_pageIndex == 0)
        {
            Refresh();
        }
        else
        {
            // Setting PageIndex will call Refresh through SetProperty.
            PageIndex = 0;
        }
    }

    private void Refresh()
    {
        CurrentPageItems.Clear();
        var start = PageIndex * _pageSize;
        for (var i = start; i < Math.Min(start + _pageSize, _source.Count); i++)
        {
            CurrentPageItems.Add(_source[i]);
        }

        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasMoreThanOnePage));
        OnPropertyChanged(nameof(PageIndicator));
        OnPropertyChanged(nameof(RangeIndicator));
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }
}

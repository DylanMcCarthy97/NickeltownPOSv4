using System.ComponentModel;
using System.Runtime.CompilerServices;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.ViewModels;

public sealed class ArchivedTabRowViewModel : INotifyPropertyChanged
{
    private string _displayLabel = string.Empty;

    private string _lastActivityText = string.Empty;

    private string _balanceLine = string.Empty;

    public ArchivedTabRowViewModel(ArchivedTabListItem source)
    {
        Source = source;
        DisplayLabel = source.DisplayLabel;
        LastActivityText = source.LastActivityText;
        BalanceLine = source.BalanceLine;
    }

    public ArchivedTabListItem Source { get; }

    public string DisplayLabel
    {
        get => _displayLabel;
        private set => SetField(ref _displayLabel, value);
    }

    public string LastActivityText
    {
        get => _lastActivityText;
        private set => SetField(ref _lastActivityText, value);
    }

    public string BalanceLine
    {
        get => _balanceLine;
        private set => SetField(ref _balanceLine, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

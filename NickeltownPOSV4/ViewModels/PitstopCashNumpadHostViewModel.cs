using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Adapts <see cref="TouchNumpadOverlayViewModel"/> for embedded <see cref="NickeltownPOSV4.Controls.TouchNumpad"/> bindings.</summary>
public sealed class PitstopCashNumpadHostViewModel : ObservableViewModel
{
    private readonly TouchNumpadOverlayViewModel _inner;

    public PitstopCashNumpadHostViewModel()
    {
        _inner = new TouchNumpadOverlayViewModel(0m, string.Empty, false, _ => { });
        _inner.PropertyChanged += OnInnerPropertyChanged;
    }

    public IRelayCommand<string> NumpadDigitCommand => _inner.DigitCommand;

    public IRelayCommand NumpadBackspaceCommand => _inner.BackspaceCommand;

    public IRelayCommand NumpadClearCommand => _inner.ClearCommand;

    public string AmountDisplay => _inner.AmountDisplay;

    public bool TryPeekCurrency(out decimal value) => _inner.TryPeekCurrency(out value);

    public void Reset(decimal initial) => _inner.ResetCurrencyDraft(initial);

    private void OnInnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TouchNumpadOverlayViewModel.AmountDisplay))
        {
            OnPropertyChanged(nameof(AmountDisplay));
        }
    }
}

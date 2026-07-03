using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class BarModeHelpPanelViewModel : ObservableViewModel
{
    private readonly ISlidePanelService _slide;

    public BarModeHelpPanelViewModel(ISlidePanelService slide)
    {
        _slide = slide;
        CloseCommand = new RelayCommand(() => _slide.Close());
    }

    public IRelayCommand CloseCommand { get; }

    public string HelpText =>
        "Tabs board (bar mode)\n\n"
        + "• Select a member tab on the 3×3 grid before Drinks, Funds, or Tab history. Guest tabs live under Guests — tap one there to select it.\n"
        + "• Selected tab panel: Drinks, Funds, History, Edit tab, and Archive (admins). Guest tabs also show Close Out.\n"
        + "• Undo last sits below the tab board (left side). You must enter your PIN to confirm an undo.\n"
        + "• Store settings are in the bottom navigation bar.\n"
        + "• Tab history: filter by month or custom dates and export a PDF to Documents\\POS Reports\\Tab history exports.\n"
        + "• Edit tab: rename, switch Member vs Guest, and edit notes.\n"
        + "• Undo last reverses the most recent bar action on this stack when supported: drinks (whole cart), funds, guest closeout payment, tab edit (name/type/notes/member link), archive/restore/new/guest/soft remove. Older drink or fund rows without a batch id cannot be undone here. If undo fails, the slot is kept so you can retry.\n";
}

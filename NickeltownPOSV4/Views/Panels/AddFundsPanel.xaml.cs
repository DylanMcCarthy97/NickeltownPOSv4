using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class AddFundsPanel : UserControl
{
    public AddFundsPanel(AddFundsPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not AddFundsPanelViewModel vm)
        {
            return;
        }

        AmountInput.GotFocus += (_, _) => vm.BeginAmountEntryCommand.Execute(null);
        NotesInput.GotFocus += (_, _) => vm.BeginNotesEntryCommand.Execute(null);
        Loaded -= OnLoaded;
    }
}

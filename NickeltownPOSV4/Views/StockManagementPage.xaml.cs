using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class StockManagementPage : Page
{
    internal readonly StockManagementPageViewModel _vm;
    internal readonly IWindowHandleProvider _windowHandleProvider;
    internal readonly IInputOverlayService _inputOverlay;

    internal Func<Task>? _stockModalPrimaryAsync;
    internal bool _stockModalDimDismissEnabled = true;
    internal bool _itemEditIsNewDraft;

    /// <summary>When true, closing item edit returns to the Product Setup list instead of the stock home screen.</summary>
    internal bool _resumeProductSetupAfterItemEdit;

    internal int _stockOverlayGate;

    internal const double StockModalFormMaxWidth = 860;
    internal const double StockSpecialModalFormMaxWidth = 720;
    internal const double StockSpecialModalMaxWidth = 1040;
    internal const double StockSpecialModalMaxHeight = 880;

    public StockManagementPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<StockManagementPageViewModel>();
        _windowHandleProvider = App.Services.GetRequiredService<IWindowHandleProvider>();
        _inputOverlay = App.Services.GetRequiredService<IInputOverlayService>();
        DataContext = _vm;
        _vm.PropertyChanged += Vm_PropertyChanged;
        Unloaded += StockManagementPage_Unloaded;
        Loaded += StockManagementPage_Loaded;
    }
}

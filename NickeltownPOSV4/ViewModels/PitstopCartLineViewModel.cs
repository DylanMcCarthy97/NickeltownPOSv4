using System;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;

namespace NickeltownPOSV4.ViewModels;

public sealed class PitstopCartLineViewModel : ObservableViewModel
{
    private int _quantity;

    public PitstopCartLineViewModel(
        long itemId,
        string displayName,
        string? sku,
        string? categoryName,
        string? subCategory,
        decimal unitPrice,
        int quantity,
        Action<PitstopCartLineViewModel> requestRemove,
        Action<PitstopCartLineViewModel, int> changeQty,
        Action<PitstopCartLineViewModel> selectLine)
    {
        ItemId = itemId;
        DisplayName = displayName;
        Sku = sku;
        CategoryName = categoryName;
        SubCategory = subCategory;
        UnitPrice = unitPrice;
        _quantity = quantity;
        _requestRemove = requestRemove;
        _changeQty = changeQty;
        _selectLine = selectLine;
        IncrementCommand = new RelayCommand(() => ChangeQty(+1), () => _quantity < 999);
        DecrementCommand = new RelayCommand(() => ChangeQty(-1));
        SelectLineCommand = new RelayCommand(() => _selectLine(this));
        RemoveCommand = new RelayCommand(() => _requestRemove(this));
    }

    private readonly Action<PitstopCartLineViewModel> _requestRemove;

    private readonly Action<PitstopCartLineViewModel, int> _changeQty;

    private readonly Action<PitstopCartLineViewModel> _selectLine;

    public long ItemId { get; }

    public string DisplayName { get; }

    public string? Sku { get; }

    public string? CategoryName { get; }

    public string? SubCategory { get; }

    public string CategoryLine
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SubCategory))
            {
                return $"{CategoryName} · {SubCategory}";
            }

            return CategoryName ?? string.Empty;
        }
    }

    public bool HasCategoryLine => !string.IsNullOrWhiteSpace(CategoryLine);

    public bool HasSku => !string.IsNullOrWhiteSpace(Sku);

    public string SkuDisplay => string.IsNullOrWhiteSpace(Sku) ? string.Empty : $"SKU {Sku.Trim()}";

    public decimal UnitPrice { get; }

    public int Quantity
    {
        get => _quantity;
        private set
        {
            if (SetProperty(ref _quantity, value))
            {
                NotifyMoneyPropertiesChanged();
                IncrementCommand.NotifyCanExecuteChanged();
                DecrementCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string UnitPriceText =>
        UnitPrice.ToString("0.00", CultureInfo.InvariantCulture);

    public string LineTotalText =>
        (UnitPrice * Quantity).ToString("0.00", CultureInfo.InvariantCulture);

    public string LineTotalDisplay => $"${LineTotalText}";

    public IRelayCommand IncrementCommand { get; }

    public IRelayCommand DecrementCommand { get; }

    public IRelayCommand SelectLineCommand { get; }

    public IRelayCommand RemoveCommand { get; }

    public void ApplyQuantity(int newQty)
    {
        if (newQty < 1)
        {
            newQty = 1;
        }

        Quantity = newQty;
    }

    private void ChangeQty(int delta)
    {
        _changeQty(this, delta);
    }

    public string UnitPriceQtyLine =>
        $"${UnitPriceText} × {Quantity.ToString(CultureInfo.InvariantCulture)}";

    internal void SyncQuantityFromHost(int q)
    {
        if (_quantity == q)
        {
            return;
        }

        _quantity = q;
        OnPropertyChanged(nameof(Quantity));
        NotifyMoneyPropertiesChanged();
        IncrementCommand.NotifyCanExecuteChanged();
        DecrementCommand.NotifyCanExecuteChanged();
    }

    private void NotifyMoneyPropertiesChanged()
    {
        OnPropertyChanged(nameof(UnitPriceText));
        OnPropertyChanged(nameof(LineTotalText));
        OnPropertyChanged(nameof(LineTotalDisplay));
        OnPropertyChanged(nameof(UnitPriceQtyLine));
    }
}

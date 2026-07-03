namespace NickeltownPOSV4.ViewModels;

public sealed class SelectedDrinkRow : ObservableViewModel
{
    private int _quantity;

    public SelectedDrinkRow(long itemId, string drinkName, decimal unitPrice)
    {
        ItemId = itemId;
        DrinkName = drinkName;
        UnitPrice = unitPrice;
        _quantity = 1;
    }

    public long ItemId { get; }

    public string DrinkName { get; }

    public decimal UnitPrice { get; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(LineSummary));
                OnPropertyChanged(nameof(UnitPriceQtyLine));
                OnPropertyChanged(nameof(QtyDisplay));
                OnPropertyChanged(nameof(LineTotalText));
            }
        }
    }

    public string LineSummary =>
        $"{DrinkName} × {Quantity} @ ${UnitPrice:0.00} = ${Quantity * UnitPrice:0.00}";

    public string UnitPriceText => $"${UnitPrice:0.00}";

    public string UnitPriceQtyLine => $"{UnitPriceText} × {Quantity}";

    public string QtyDisplay => $"{Quantity}×";

    public string LineTotalText => $"${Quantity * UnitPrice:0.00}";
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Editable row for the stock category admin dialog (rename / reorder / delete).</summary>
public sealed class CategoryRowModel : ObservableObject
{
    private string _name;

    public CategoryRowModel(long id, string name, int sortOrder)
    {
        Id = id;
        _name = name;
        SortOrder = sortOrder;
    }

    public long Id { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int SortOrder { get; set; }
}

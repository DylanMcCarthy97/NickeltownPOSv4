using System;
using System.ComponentModel;
using System.Globalization;
using NickeltownPOSV4.Services.Tabs;

namespace NickeltownPOSV4.Models;

/// <summary>One club tab tile for the tabs board.</summary>
public sealed class TabCardModel : INotifyPropertyChanged
{
    public TabCardModel(
        string id,
        string rawName,
        decimal balance,
        string lastDrinkLine,
        bool isMember = true,
        bool isGuest = false,
        string? lastActivityAt = null,
        int openDrinkCount = 0)
    {
        Id = id;
        RawName = rawName;
        Balance = balance;
        LastDrinkLine = lastDrinkLine;
        IsMember = isMember;
        IsGuest = isGuest;
        LastActivityAt = lastActivityAt;
        OpenDrinkCount = openDrinkCount;
    }

    public string Id { get; }

    public string RawName { get; }

    public string DisplayName => FormatTabName(RawName);

    public decimal Balance { get; }

    public string LastDrinkLine { get; }

    public string? LastActivityAt { get; }

    public int OpenDrinkCount { get; }

    public bool IsMember { get; }

    public bool IsGuest { get; }

    public string MemberBadge =>
        IsGuest ? "GUEST" :
        IsMember ? "MEMBER" : "ACCOUNT";

    public string LastUpdatedText => TabsBoardActivityFormatter.FormatRelative(LastActivityAt);

    public string FooterLine => TabsBoardCardFooterFormatter.Format(OpenDrinkCount, LastActivityAt);

    public string BalanceStatusLabel =>
        Balance < 0m ? "OWING" :
        Balance == 0m ? "SETTLED" :
        Balance < 10m ? "LOW CREDIT" :
        "CREDIT";

    public TabBalanceTier BalanceTier =>
        Balance < 0m ? TabBalanceTier.Negative :
        Balance == 0m ? TabBalanceTier.Settled :
        Balance < 10m ? TabBalanceTier.Low :
        TabBalanceTier.Good;

    public string BalanceText
    {
        get
        {
            var prefix = Balance < 0m ? "-" : string.Empty;
            return prefix + "$" + Math.Abs(Balance).ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Same rule as legacy MainForm.FormatTabName.</summary>
    public static string FormatTabName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        return name.Replace("_", " & ");
    }
}

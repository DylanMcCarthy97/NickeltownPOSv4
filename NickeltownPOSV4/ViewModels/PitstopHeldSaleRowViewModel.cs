using System;
using System.Globalization;

namespace NickeltownPOSV4.ViewModels;

public sealed class PitstopHeldSaleRowViewModel
{
    private const string Sep = " \u00b7 ";

    public PitstopHeldSaleRowViewModel(
        long id,
        DateTimeOffset heldAt,
        int lineCount,
        decimal totalAmount,
        string? staffDisplayName)
    {
        Id = id;
        HeldAt = heldAt;
        LineCount = lineCount;
        TotalAmount = totalAmount;
        StaffDisplayName = staffDisplayName;
    }

    public long Id { get; }

    public DateTimeOffset HeldAt { get; }

    public int LineCount { get; }

    public decimal TotalAmount { get; }

    public string? StaffDisplayName { get; }

    public string DisplayLabel =>
        $"{LineCount} item{(LineCount == 1 ? string.Empty : "s")}";

    public string BalanceLine =>
        $"${TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)}";

    public string HeldAtText =>
        HeldAt.ToLocalTime().ToString("h:mm tt", CultureInfo.CurrentCulture);

    public string SubtitleText
    {
        get
        {
            var staff = string.IsNullOrWhiteSpace(StaffDisplayName) ? null : StaffDisplayName.Trim();
            return staff is null ? HeldAtText : $"{HeldAtText}{Sep}{staff}";
        }
    }
}

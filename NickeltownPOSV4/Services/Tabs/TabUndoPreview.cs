using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;

namespace NickeltownPOSV4.Services.Tabs;

/// <summary>Details shown on the Undo Last Transaction confirmation dialog.</summary>
public sealed class TabUndoPreview
{
    public required string Headline { get; init; }

    public string? AmountText { get; init; }

    public string? DetailLine { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Short operator hint after a successful undo.</summary>
    public required string Description { get; init; }

    public string TimeText => FormatTime(OccurredAt);

    public string? ActionKind { get; init; }

    public const string ComplimentaryActionKind = "ComplimentaryItem";

    public static TabUndoPreview ForDrinks(
        IReadOnlyList<TabDrinkSaleLine> lines,
        string tabName,
        DateTimeOffset? occurredAt = null)
    {
        var headline = FormatDrinkHeadline(lines);
        var total = lines.Sum(l => l.UnitPrice * l.Quantity);
        var name = NormalizeName(tabName);
        return new TabUndoPreview
        {
            Headline = headline,
            AmountText = FormatMoney(total),
            DetailLine = "Added to " + name,
            OccurredAt = occurredAt ?? DateTimeOffset.Now,
            Description = headline.Replace("\n", ", ", StringComparison.Ordinal),
        };
    }

    public static TabUndoPreview ForComplimentaryItem(
        string itemName,
        int quantity,
        DateTimeOffset? occurredAt = null)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        var qty = quantity <= 0 ? 1 : quantity;
        var headline = $"Free {name} \u00d7{qty.ToString(CultureInfo.InvariantCulture)}";
        return new TabUndoPreview
        {
            Headline = headline,
            AmountText = null,
            DetailLine = "Complimentary member item",
            OccurredAt = occurredAt ?? DateTimeOffset.Now,
            Description = headline,
            ActionKind = ComplimentaryActionKind,
        };
    }

    public static TabUndoPreview ForFunds(
        string actionLabel,
        decimal amount,
        string tabName,
        DateTimeOffset? occurredAt = null)
    {
        var label = string.IsNullOrWhiteSpace(actionLabel) ? "Funds" : actionLabel.Trim();
        var name = NormalizeName(tabName);
        return new TabUndoPreview
        {
            Headline = label,
            AmountText = FormatMoney(amount),
            DetailLine = "Added to " + name,
            OccurredAt = occurredAt ?? DateTimeOffset.Now,
            Description = $"{label} {FormatMoney(amount)}",
        };
    }

    public static TabUndoPreview ForTabAction(
        string headline,
        string detailLine,
        string? description = null,
        DateTimeOffset? occurredAt = null)
    {
        var head = string.IsNullOrWhiteSpace(headline) ? "Tab action" : headline.Trim();
        var detail = string.IsNullOrWhiteSpace(detailLine) ? null : detailLine.Trim();
        return new TabUndoPreview
        {
            Headline = head,
            AmountText = null,
            DetailLine = detail,
            OccurredAt = occurredAt ?? DateTimeOffset.Now,
            Description = string.IsNullOrWhiteSpace(description) ? head : description.Trim(),
        };
    }

    public static TabUndoPreview FromDescription(string description, DateTimeOffset? occurredAt = null)
    {
        var desc = string.IsNullOrWhiteSpace(description) ? "Undo last tab action" : description.Trim();
        return new TabUndoPreview
        {
            Headline = desc,
            AmountText = null,
            DetailLine = null,
            OccurredAt = occurredAt ?? DateTimeOffset.Now,
            Description = desc,
        };
    }

    public static string FormatDrinkHeadline(IReadOnlyList<TabDrinkSaleLine> lines)
    {
        if (lines is null || lines.Count == 0)
        {
            return "Drinks";
        }

        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            var name = string.IsNullOrWhiteSpace(line.DisplayName) ? "Drink" : line.DisplayName.Trim();
            var qty = line.Quantity <= 0 ? 1 : line.Quantity;
            sb.Append(name).Append(" \u00d7").Append(qty.ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    public static string FormatMoney(decimal value) => ActivityLogText.Money(value);

    public static string FormatTime(DateTimeOffset occurredAt) =>
        occurredAt.ToString("h:mm tt", CultureInfo.InvariantCulture);

    private static string NormalizeName(string? tabName) =>
        string.IsNullOrWhiteSpace(tabName) ? "tab" : tabName.Trim();
}

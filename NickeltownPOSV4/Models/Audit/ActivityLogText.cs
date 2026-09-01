using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NickeltownPOSV4.Models.Audit;

/// <summary>
/// Human-readable Activity Log lines, e.g. <c>7:42pm | Dylan | Undid 1 × Great Northern | Smith tab</c>.
/// </summary>
public static class ActivityLogText
{
    public static readonly IReadOnlyList<string> StaffFacingActionTypes =
    [
        AuditActions.TabFundsAdded,
        AuditActions.TabFundsUndone,
        AuditActions.TabFundsUndoneSquareWarning,
        AuditActions.TabPurchaseUndone,
        AuditActions.TabArchived,
        AuditActions.TabRestored,
        AuditActions.TabRemoved,
        AuditActions.TabErased,
        AuditActions.TabClosed,
        AuditActions.StockManuallyAdjusted,
        AuditActions.StockRestored,
        AuditActions.SaleVoided,
        AuditActions.SquareRecoveryLinkedPitstop,
        AuditActions.SquareRecoveryLinkedTab,
        AuditActions.SquareRecoveryManuallyReconciled,
        AuditActions.SquareRecoveryNoteAdded,
        AuditActions.PaymentRecoveryGenerated,
        AuditActions.PermissionDenied,
        AuditActions.BackupCreated,
        AuditActions.BackupFailed,
        AuditActions.PitstopArchived,
        AuditActions.PitstopEodExported,
        AuditActions.PitstopArchiveNoteAdded,
        AuditActions.MembershipSettingsUpdated,
        AuditActions.ComplimentaryItemRecorded,
        AuditActions.ComplimentaryItemUndone,
        AuditActions.QuickFreeItemAdded,
        AuditActions.QuickFreeItemRemoved,
        AuditActions.QuickFreeItemConfigChanged,
    ];

    public static bool IsStaffFacing(string? actionType) =>
        !string.IsNullOrWhiteSpace(actionType)
        && StaffFacingActionTypes.Contains(actionType.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string FormatLine(AuditLogEntry entry, DateTimeOffset nowLocal)
    {
        var time = FormatTime(entry.OccurredAt, nowLocal);
        var staff = string.IsNullOrWhiteSpace(entry.StaffName) ? "Unknown" : entry.StaffName.Trim();
        var action = FormatAction(entry);
        var line = $"{time} | {staff} | {action}";
        return entry.Success ? line : line + " (failed)";
    }

    public static string FormatTime(DateTimeOffset occurredAt, DateTimeOffset nowLocal)
    {
        var local = occurredAt.ToLocalTime();
        var today = nowLocal.Date;
        var time = local.ToString("h:mmtt", CultureInfo.InvariantCulture).ToLowerInvariant();
        if (local.Date == today)
        {
            return time;
        }

        if (local.Date == today.AddDays(-1))
        {
            return "yesterday " + time;
        }

        if (local.Year == today.Year)
        {
            return local.ToString("d MMM", CultureInfo.InvariantCulture) + " " + time;
        }

        return local.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + " " + time;
    }

    public static string FormatAction(AuditLogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Reason))
        {
            return entry.Reason.Trim();
        }

        return FallbackAction(entry.ActionType);
    }

    public static string TabClause(string? tabName)
    {
        var name = NormalizeTabName(tabName);
        return string.IsNullOrEmpty(name) ? string.Empty : " | " + name + " tab";
    }

    public static string NormalizeTabName(string? tabName)
    {
        var name = (tabName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return string.Empty;
        }

        if (name.EndsWith(" tab", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4].Trim();
        }

        return name;
    }

    public static string Money(decimal amount)
    {
        var abs = Math.Abs(amount).ToString("0.00", CultureInfo.InvariantCulture);
        return amount < 0m ? "-$" + abs : "$" + abs;
    }

    public static string UndidDrinks(IReadOnlyList<(string Name, int Quantity)> lines, string? tabName)
    {
        var parts = lines
            .Where(l => l.Quantity > 0)
            .Select(l =>
            {
                var n = string.IsNullOrWhiteSpace(l.Name) ? "item" : l.Name.Trim();
                return $"{l.Quantity} × {n}";
            })
            .ToList();

        var body = parts.Count switch
        {
            0 => "Undid drinks",
            1 => "Undid " + parts[0],
            <= 3 => "Undid " + string.Join(", ", parts),
            _ => $"Undid {parts.Count} drink lines",
        };

        return body + TabClause(tabName);
    }

    public static string FundsAdded(string movementKey, decimal amount, string? tabName)
    {
        var key = (movementKey ?? string.Empty).Trim().ToLowerInvariant();
        var money = Money(amount);
        var body = key switch
        {
            "cash" => $"Added {money} cash",
            "square" => $"Added {money} Square card",
            "raffle" => $"Added {money} raffle",
            "reimburse" => $"Added {money} reimbursement",
            "manual" => amount < 0m
                ? $"Adjusted balance {money}"
                : $"Adjusted balance +{money}",
            "correction" => $"Corrected balance {money}",
            _ => amount < 0m ? $"Altered payment {money}" : $"Added {money} funds",
        };

        return body + TabClause(tabName);
    }

    public static string FundsUndone(decimal amount, string? tabName, string? movementKey = null)
    {
        var key = (movementKey ?? string.Empty).Trim().ToLowerInvariant();
        var money = Money(Math.Abs(amount));
        var body = key switch
        {
            "square" => $"Undid {money} Square card",
            "cash" => $"Undid {money} cash",
            "manual" => $"Undid {money} balance adjustment",
            "correction" => $"Undid {money} correction",
            "raffle" => $"Undid {money} raffle",
            "reimburse" => $"Undid {money} reimbursement",
            _ => $"Undid {money} funds",
        };

        return body + TabClause(tabName);
    }

    public static string ArchivedTab(string? tabName) => "Archived" + NamedTab(tabName);

    public static string ReopenedTab(string? tabName) => "Reopened" + NamedTab(tabName);

    public static string RemovedTab(string? tabName) => "Removed" + NamedTab(tabName);

    public static string RestoredTab(string? tabName) => "Restored" + NamedTab(tabName);

    public static string ErasedTab(string? tabName) => "Permanently erased" + NamedTab(tabName);

    public static string ClosedGuestTabs(int count, string? singleTabName = null)
    {
        if (count <= 0)
        {
            return "Closed guest tabs";
        }

        if (count == 1)
        {
            return "Closed" + NamedTab(singleTabName ?? "guest");
        }

        return $"Closed {count} guest tabs";
    }

    public static string ArchivedGuestTabs(int count) =>
        count <= 1 ? "Archived guest tabs" : $"Archived {count} guest tabs";

    public static string StockAdjusted(string? itemName, int oldQty, int newQty)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        return $"Adjusted {name} stock {oldQty} → {newQty}";
    }

    public static string StockReceived(string? itemName, int qty)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        return $"Received {qty} × {name}";
    }

    public static string StockCounted(int changedItemCount)
    {
        if (changedItemCount <= 0)
        {
            return "Completed stock count";
        }

        return changedItemCount == 1
            ? "Stock count adjusted 1 item"
            : $"Stock count adjusted {changedItemCount} items";
    }

    public static string StockItemDeleted(string? itemName)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        return $"Deleted stock item {name}";
    }

    public static string ComplimentaryItemRecorded(string? itemName, int quantity)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        var qty = quantity <= 0 ? 1 : quantity;
        return $"Recorded {qty} × {name} as a free member item";
    }

    public static string ComplimentaryItemUndone(string? itemName, int quantity)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        var qty = quantity <= 0 ? 1 : quantity;
        return $"Undid {qty} × {name} free member item";
    }

    public static string QuickFreeItemAdded(string? itemName)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        return $"Added {name} to Quick Free Items";
    }

    public static string QuickFreeItemRemoved(string? itemName)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        return $"Removed {name} from Quick Free Items";
    }

    public static string QuickFreeItemConfigChanged(string? itemName)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        return $"Changed Quick Free Item {name}";
    }

    private static string NamedTab(string? tabName)
    {
        var name = NormalizeTabName(tabName);
        return string.IsNullOrEmpty(name) ? " a tab" : " " + name + " tab";
    }

    private static string FallbackAction(string? actionType) =>
        (actionType ?? string.Empty).Trim() switch
        {
            AuditActions.TabFundsAdded => "Added funds",
            AuditActions.TabFundsUndone => "Undid funds",
            AuditActions.TabFundsUndoneSquareWarning => "Undid Square top-up (POS only)",
            AuditActions.TabPurchaseUndone => "Undid a purchase",
            AuditActions.TabArchived => "Archived a tab",
            AuditActions.TabRestored => "Reopened a tab",
            AuditActions.TabRemoved => "Removed a tab",
            AuditActions.TabErased => "Permanently erased a tab",
            AuditActions.TabClosed => "Closed guest tabs",
            AuditActions.StockManuallyAdjusted => "Changed stock",
            AuditActions.StockRestored => "Restored stock",
            AuditActions.SaleVoided => "Voided a sale",
            AuditActions.SquareRecoveryLinkedPitstop => "Linked Square payment to Pitstop sale",
            AuditActions.SquareRecoveryLinkedTab => "Linked Square payment to a tab",
            AuditActions.SquareRecoveryManuallyReconciled => "Reconciled Square payment",
            AuditActions.SquareRecoveryNoteAdded => "Added Square recovery note",
            AuditActions.PaymentRecoveryGenerated => "Generated payment recovery",
            AuditActions.PermissionDenied => "Permission denied",
            AuditActions.BackupCreated => "Created backup",
            AuditActions.BackupFailed => "Backup failed",
            AuditActions.PitstopArchived => "Archived Pitstop",
            AuditActions.PitstopEodExported => "Exported Pitstop EOD",
            AuditActions.PitstopArchiveNoteAdded => "Added Pitstop archive note",
            AuditActions.MembershipSettingsUpdated => "Updated membership settings",
            AuditActions.ComplimentaryItemRecorded => "Recorded a free member item",
            AuditActions.ComplimentaryItemUndone => "Undid a free member item",
            AuditActions.QuickFreeItemAdded => "Added a Quick Free Item",
            AuditActions.QuickFreeItemRemoved => "Removed a Quick Free Item",
            AuditActions.QuickFreeItemConfigChanged => "Changed Quick Free Items",
            _ => string.IsNullOrWhiteSpace(actionType) ? "Activity" : actionType.Trim(),
        };
}

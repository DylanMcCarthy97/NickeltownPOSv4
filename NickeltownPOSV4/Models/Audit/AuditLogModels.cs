using System;

namespace NickeltownPOSV4.Models.Audit;

public static class AuditActions
{
    // Sale lifecycle
    public const string SaleCreated = "sale.created";
    public const string SaleVoided = "sale.voided";

    // Tab funds
    public const string TabFundsAdded = "tab.funds_added";
    public const string TabFundsUndone = "tab.funds_undone";
    public const string TabFundsUndoneSquareWarning = "tab.funds_undone_square_warning";

    // Tab lifecycle / drink undo
    public const string TabPurchaseUndone = "tab.purchase_undone";
    public const string TabArchived = "tab.archived";
    public const string TabRestored = "tab.restored";
    public const string TabRemoved = "tab.removed";
    public const string TabErased = "tab.erased";
    public const string TabClosed = "tab.closed";

    // Payment pipeline (Pitstop / Square)
    public const string PaymentStarted = "payment.started";
    public const string PaymentSent = "payment.sent";
    public const string PaymentApproved = "payment.approved";
    public const string PaymentDeclined = "payment.declined";
    public const string PaymentSaleSaved = "payment.sale_saved";
    public const string PaymentRecoveryGenerated = "payment.recovery_generated";

    // Square payments
    public const string SquareAttemptStarted = "square.attempt_started";
    public const string SquareAttemptApproved = "square.attempt_approved";
    public const string SquareAttemptCompleted = "square.attempt_completed";
    public const string SquareAttemptFailed = "square.attempt_failed";
    public const string SquareAttemptMarkCompletedFailed = "square.attempt_mark_completed_failed";
    public const string SquareRecoveryLinkedPitstop = "square.recovery_linked_pitstop";
    public const string SquareRecoveryLinkedTab = "square.recovery_linked_tab";
    public const string SquareRecoveryManuallyReconciled = "square.recovery_manually_reconciled";
    public const string SquareRecoveryNoteAdded = "square.recovery_note_added";

    // Stock
    public const string StockDeducted = "stock.deducted";
    public const string StockRestored = "stock.restored";
    public const string StockManuallyAdjusted = "stock.manually_adjusted";

    // EOD / archive
    public const string PitstopEodExported = "pitstop.eod_exported";
    public const string PitstopArchived = "pitstop.archived";
    public const string PitstopArchiveNoteAdded = "pitstop.archive_note_added";

    // Backups
    public const string BackupCreated = "backup.created";
    public const string BackupFailed = "backup.failed";

    // Membership
    public const string MembershipSettingsUpdated = "membership.settings_updated";

    // Complimentary / free member items
    public const string ComplimentaryItemRecorded = "complimentary.item_recorded";
    public const string ComplimentaryItemUndone = "complimentary.item_undone";
    public const string QuickFreeItemAdded = "complimentary.config_added";
    public const string QuickFreeItemRemoved = "complimentary.config_removed";
    public const string QuickFreeItemConfigChanged = "complimentary.config_changed";

    // Permissions
    public const string PermissionDenied = "permission.denied";
}

public static class AuditEntityTypes
{
    public const string PitstopSale = "PitstopSale";
    public const string Tab = "Tab";
    public const string SquareAttempt = "SquareAttempt";
    public const string PitstopEodBatch = "PitstopEodBatch";
    public const string Stock = "Stock";
    public const string Backup = "Backup";
    public const string System = "System";
    public const string MembershipSettings = "MembershipSettings";
    public const string MembershipApplication = "MembershipApplication";
    public const string MembershipMember = "MembershipMember";
    public const string ComplimentaryItem = "ComplimentaryItem";
    public const string QuickFreeItemConfig = "QuickFreeItemConfig";
}

public sealed class AuditLogEntryRequest
{
    public string ActionType { get; init; } = string.Empty;

    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public decimal? Amount { get; init; }

    public string? Reason { get; init; }

    public bool Success { get; init; } = true;

    public string? DetailsJson { get; init; }
}

public sealed class AuditLogEntry
{
    public long Id { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public long? StaffId { get; init; }

    public string? StaffName { get; init; }

    public string? StaffRole { get; init; }

    public string ActionType { get; init; } = string.Empty;

    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public decimal? Amount { get; init; }

    public string? Reason { get; init; }

    public bool Success { get; init; }

    public string? DetailsJson { get; init; }
}

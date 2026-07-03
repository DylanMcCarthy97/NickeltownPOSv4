using System;
using System.Runtime.Serialization;

namespace NickeltownPOSV4.Services.Treasurer
{
    /// <summary>Balance source for an account: manual entry or calculated from transactions.</summary>
    public enum TreasurerBalanceSource
    {
        Manual = 0,
        FromTransactions = 1
    }

    /// <summary>Transaction type: Income, Expense, Transfer (between accounts), or Adjustment.</summary>
    public enum TreasurerTransactionType
    {
        Income = 0,
        Expense = 1,
        Transfer = 2,
        Adjustment = 3
    }

    /// <summary>Category type for reporting: Income or Expense.</summary>
    public enum TreasurerCategoryType
    {
        Income = 0,
        Expense = 1
    }

    [DataContract]
    public class TreasurerAccount
    {
        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();
        [DataMember] public string Name { get; set; } = string.Empty;
        [DataMember] public decimal OpeningBalance { get; set; }
        [DataMember] public decimal CurrentBalance { get; set; }
        [DataMember] public int BalanceSource { get; set; } // 0 = Manual, 1 = FromTransactions
        [DataMember] public string Notes { get; set; } = string.Empty;
        [DataMember] public bool IsActive { get; set; } = true;
        [DataMember] public int SortOrder { get; set; }
    }

    [DataContract]
    public class TreasurerTransaction
    {
        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();
        [DataMember] public DateTime Date { get; set; }
        [DataMember] public int Type { get; set; } // 0=Income, 1=Expense, 2=Transfer, 3=Adjustment
        [DataMember] public decimal Amount { get; set; }
        [DataMember] public Guid? CategoryId { get; set; }
        [DataMember] public string Description { get; set; } = string.Empty;
        [DataMember] public Guid AccountId { get; set; }
        [DataMember] public Guid? ToAccountId { get; set; } // For Transfer only
        [DataMember] public string PaymentMethod { get; set; } = string.Empty;
        [DataMember] public string Reference { get; set; } = string.Empty;
        [DataMember] public string Notes { get; set; } = string.Empty;
        [DataMember] public string EnteredBy { get; set; } = string.Empty;
        [DataMember] public DateTime CreatedAt { get; set; }
        [DataMember] public DateTime ModifiedAt { get; set; }
        [DataMember] public bool IsVoided { get; set; }
        [DataMember] public DateTime? VoidedAt { get; set; }
        [DataMember] public string VoidedBy { get; set; } = string.Empty;
        /// <summary>Origin of transaction: empty/Manual, or BankImport.</summary>
        [DataMember] public string Source { get; set; } = string.Empty;
    }

    /// <summary>Rule to auto-assign category when bank import description contains pattern (case-insensitive).</summary>
    [DataContract]
    public class BankImportRule
    {
        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();
        [DataMember] public string Pattern { get; set; } = string.Empty;
        [DataMember] public Guid CategoryId { get; set; }
        [DataMember] public int SortOrder { get; set; }
    }

    /// <summary>Match status for a bank transaction against internal ledger.</summary>
    public enum BankTransactionMatchStatus
    {
        Unmatched = 0,
        AutoSuggested = 1,
        ManuallyMatched = 2,
        Reconciled = 3,
        NeedsReview = 4,
        Ignored = 5
    }

    /// <summary>One import batch of bank statement rows (one CSV file).</summary>
    [DataContract]
    public class TreasurerBankImportBatch
    {
        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();
        [DataMember] public Guid AccountId { get; set; }
        [DataMember] public string FileName { get; set; } = string.Empty;
        [DataMember] public string ImportedBy { get; set; } = string.Empty;
        [DataMember] public DateTime ImportedAt { get; set; }
        [DataMember] public DateTime? StatementStartDate { get; set; }
        [DataMember] public DateTime? StatementEndDate { get; set; }
    }

    /// <summary>Single row from an imported bank statement; reconciled against internal transactions.</summary>
    [DataContract]
    public class TreasurerBankTransaction
    {
        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();
        [DataMember] public Guid ImportBatchId { get; set; }
        [DataMember] public Guid AccountId { get; set; }
        [DataMember] public DateTime TransactionDate { get; set; }
        [DataMember] public string Description { get; set; } = string.Empty;
        [DataMember] public decimal Amount { get; set; }  // Positive = credit, negative = debit
        [DataMember] public decimal? Balance { get; set; }  // Running balance from statement if available
        [DataMember] public string Reference { get; set; } = string.Empty;
        [DataMember] public Guid? SuggestedCategoryId { get; set; }
        [DataMember] public int MatchStatus { get; set; }   // BankTransactionMatchStatus
        [DataMember] public Guid? MatchedTransactionId { get; set; }  // Confirmed internal transaction
        [DataMember] public Guid? SuggestedMatchTransactionId { get; set; }  // Auto-suggested internal tx
        [DataMember] public int MatchConfidence { get; set; }  // 0-100
        [DataMember] public string MatchedBy { get; set; } = string.Empty;
        [DataMember] public DateTime? MatchedAt { get; set; }
        [DataMember] public bool IsReconciled { get; set; }
        [DataMember] public string ReconciledBy { get; set; } = string.Empty;
        [DataMember] public DateTime? ReconciledAt { get; set; }
        [DataMember] public bool IsIgnored { get; set; }
        [DataMember] public string IgnoredBy { get; set; } = string.Empty;
        [DataMember] public DateTime? IgnoredAt { get; set; }
        [DataMember] public Guid? CreatedInternalTransactionId { get; set; }  // If internal tx was created from this bank tx
    }

    /// <summary>Audit log for reconciliation actions: match, unmatch, reconcile, create internal, ignore.</summary>
    [DataContract]
    public class TreasurerReconciliationAuditEntry
    {
        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();
        [DataMember] public DateTime Timestamp { get; set; }
        [DataMember] public string UserName { get; set; } = string.Empty;
        [DataMember] public string Action { get; set; } = string.Empty;  // Matched, Unmatched, Reconciled, CreatedInternal, Ignored, Unignored
        [DataMember] public Guid BankTransactionId { get; set; }
        [DataMember] public Guid? InternalTransactionId { get; set; }
        [DataMember] public string OldValue { get; set; } = string.Empty;
        [DataMember] public string NewValue { get; set; } = string.Empty;
    }

    [DataContract]
    public class TreasurerCategory
    {
        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();
        [DataMember] public string Name { get; set; } = string.Empty;
        [DataMember] public int Type { get; set; } // 0=Income, 1=Expense
        [DataMember] public bool IsArchived { get; set; }
        [DataMember] public int SortOrder { get; set; }
    }

    [DataContract]
    public class TreasurerAuditEntry
    {
        [DataMember] public Guid Id { get; set; } = Guid.NewGuid();
        [DataMember] public DateTime Timestamp { get; set; }
        [DataMember] public string UserName { get; set; } = string.Empty;
        [DataMember] public string Action { get; set; } = string.Empty; // Created, Updated, Voided
        [DataMember] public string EntityType { get; set; } = string.Empty; // Transaction, Account, Category
        [DataMember] public string EntityId { get; set; } = string.Empty;
        [DataMember] public string OldValue { get; set; } = string.Empty;
        [DataMember] public string NewValue { get; set; } = string.Empty;
    }
}


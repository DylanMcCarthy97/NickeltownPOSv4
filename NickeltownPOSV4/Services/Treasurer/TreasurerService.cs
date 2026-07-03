using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace NickeltownPOSV4.Services.Treasurer
{
    /// <summary>Loads/saves treasurer data (accounts, transactions, categories, audit) and computes balances.</summary>
    public static class TreasurerService
    {
        private static string AccountsPath => TreasurerDataPaths.GetDataFilePath("treasurer_accounts.json");
        private static string TransactionsPath => TreasurerDataPaths.GetDataFilePath("treasurer_transactions.json");
        private static string CategoriesPath => TreasurerDataPaths.GetDataFilePath("treasurer_categories.json");
        private static string AuditPath => TreasurerDataPaths.GetDataFilePath("treasurer_audit.json");
        private static string BankImportRulesPath => TreasurerDataPaths.GetDataFilePath("treasurer_bank_import_rules.json");
        private static string BankBatchesPath => TreasurerDataPaths.GetDataFilePath("treasurer_bank_batches.json");
        private static string BankTransactionsPath => TreasurerDataPaths.GetDataFilePath("treasurer_bank_transactions.json");
        private static string ReconciliationAuditPath => TreasurerDataPaths.GetDataFilePath("treasurer_reconciliation_audit.json");

        private static readonly object _accountsLock = new object();
        private static readonly object _transactionsLock = new object();
        private static readonly object _categoriesLock = new object();
        private static readonly object _auditLock = new object();
        private static readonly object _bankRulesLock = new object();
        private static readonly object _bankBatchesLock = new object();
        private static readonly object _bankTransactionsLock = new object();
        private static readonly object _reconciliationAuditLock = new object();

        // --- Accounts ---
        public static List<TreasurerAccount> GetAccounts()
        {
            lock (_accountsLock)
            {
                return LoadList<List<TreasurerAccount>>(AccountsPath) ?? new List<TreasurerAccount>();
            }
        }

        public static void SaveAccounts(List<TreasurerAccount> list)
        {
            lock (_accountsLock)
            {
                SaveList(AccountsPath, list ?? new List<TreasurerAccount>());
            }
        }

        /// <summary>Get current balance for an account. If BalanceSource is FromTransactions, compute from opening + transactions.</summary>
        public static decimal GetAccountBalance(Guid accountId, List<TreasurerTransaction> allTransactions = null)
        {
            var accounts = GetAccounts();
            var acc = accounts.FirstOrDefault(a => a.Id == accountId);
            if (acc == null) return 0;
            if (acc.BalanceSource == (int)TreasurerBalanceSource.Manual)
                return acc.CurrentBalance;
            var txList = allTransactions ?? GetTransactions();
            return ComputeBalanceForAccount(acc, txList);
        }

        private static decimal ComputeBalanceForAccount(TreasurerAccount acc, List<TreasurerTransaction> txList)
        {
            decimal balance = acc.OpeningBalance;
            foreach (var t in txList.Where(x => !x.IsVoided).OrderBy(x => x.Date).ThenBy(x => x.CreatedAt))
            {
                if (t.Type == (int)TreasurerTransactionType.Transfer)
                {
                    if (t.AccountId == acc.Id) balance -= t.Amount;
                    if (t.ToAccountId == acc.Id) balance += t.Amount;
                }
                else if (t.Type == (int)TreasurerTransactionType.Income || t.Type == (int)TreasurerTransactionType.Adjustment)
                {
                    if (t.AccountId == acc.Id) balance += t.Amount;
                }
                else if (t.Type == (int)TreasurerTransactionType.Expense)
                {
                    if (t.AccountId == acc.Id) balance -= t.Amount;
                }
            }
            return balance;
        }

        /// <summary>Refresh CurrentBalance on all accounts that use FromTransactions.</summary>
        public static void RefreshComputedBalances()
        {
            var accounts = GetAccounts();
            var txList = GetTransactions();
            foreach (var acc in accounts.Where(a => a.BalanceSource == (int)TreasurerBalanceSource.FromTransactions))
            {
                acc.CurrentBalance = ComputeBalanceForAccount(acc, txList);
            }
            SaveAccounts(accounts);
        }

        // --- Transactions ---
        public static List<TreasurerTransaction> GetTransactions(
            DateTime? from = null, DateTime? to = null,
            Guid? accountId = null, Guid? categoryId = null, int? type = null,
            decimal? amountMin = null, decimal? amountMax = null,
            string search = null)
        {
            lock (_transactionsLock)
            {
                var list = LoadList<List<TreasurerTransaction>>(TransactionsPath) ?? new List<TreasurerTransaction>();
                if (from.HasValue) list = list.Where(t => t.Date.Date >= from.Value.Date).ToList();
                if (to.HasValue) list = list.Where(t => t.Date.Date <= to.Value.Date).ToList();
                if (accountId.HasValue)
                    list = list.Where(t => t.AccountId == accountId.Value || t.ToAccountId == accountId.Value).ToList();
                if (categoryId.HasValue) list = list.Where(t => t.CategoryId == categoryId.Value).ToList();
                if (type.HasValue) list = list.Where(t => t.Type == type.Value).ToList();
                if (amountMin.HasValue) list = list.Where(t => t.Amount >= amountMin.Value).ToList();
                if (amountMax.HasValue) list = list.Where(t => t.Amount <= amountMax.Value).ToList();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLowerInvariant();
                    list = list.Where(t =>
                        (t.Description ?? "").ToLowerInvariant().Contains(s) ||
                        (t.Reference ?? "").ToLowerInvariant().Contains(s) ||
                        (t.Notes ?? "").ToLowerInvariant().Contains(s)).ToList();
                }
                return list.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).ToList();
            }
        }

        /// <summary>Get all transactions (no filter). Used for balance computation and full ledger.</summary>
        public static List<TreasurerTransaction> GetTransactions()
        {
            lock (_transactionsLock)
            {
                return LoadList<List<TreasurerTransaction>>(TransactionsPath) ?? new List<TreasurerTransaction>();
            }
        }

        public static void AddTransaction(TreasurerTransaction t, string enteredBy)
        {
            t.EnteredBy = enteredBy;
            t.CreatedAt = t.ModifiedAt = DateTime.Now;
            t.IsVoided = false;
            lock (_transactionsLock)
            {
                var list = LoadList<List<TreasurerTransaction>>(TransactionsPath) ?? new List<TreasurerTransaction>();
                list.Add(t);
                SaveList(TransactionsPath, list);
            }
            RefreshComputedBalances();
            AppendAudit(enteredBy, "Created", "Transaction", t.Id.ToString(), "", TransactionSummary(t));
        }

        public static void UpdateTransaction(TreasurerTransaction t, string modifiedBy, string oldSummary)
        {
            t.ModifiedAt = DateTime.Now;
            lock (_transactionsLock)
            {
                var list = LoadList<List<TreasurerTransaction>>(TransactionsPath) ?? new List<TreasurerTransaction>();
                var idx = list.FindIndex(x => x.Id == t.Id);
                if (idx >= 0) list[idx] = t;
                SaveList(TransactionsPath, list);
            }
            RefreshComputedBalances();
            AppendAudit(modifiedBy, "Updated", "Transaction", t.Id.ToString(), oldSummary ?? "", TransactionSummary(t));
        }

        public static void VoidTransaction(Guid id, string voidedBy)
        {
            lock (_transactionsLock)
            {
                var list = LoadList<List<TreasurerTransaction>>(TransactionsPath) ?? new List<TreasurerTransaction>();
                var t = list.FirstOrDefault(x => x.Id == id);
                if (t == null) return;
                var oldSummary = TransactionSummary(t);
                t.IsVoided = true;
                t.VoidedAt = DateTime.Now;
                t.VoidedBy = voidedBy;
                t.ModifiedAt = DateTime.Now;
                SaveList(TransactionsPath, list);
                RefreshComputedBalances();
                AppendAudit(voidedBy, "Voided", "Transaction", t.Id.ToString(), oldSummary, "(voided)");
            }
        }

        private static string TransactionSummary(TreasurerTransaction t)
        {
            return $"{t.Date:yyyy-MM-dd} {((TreasurerTransactionType)t.Type)} {t.Amount:N2} {t.Description}";
        }

        // --- Categories ---
        public static List<TreasurerCategory> GetCategories(bool includeArchived = false)
        {
            lock (_categoriesLock)
            {
                var list = LoadList<List<TreasurerCategory>>(CategoriesPath) ?? new List<TreasurerCategory>();
                if (!includeArchived) list = list.Where(c => !c.IsArchived).ToList();
                return list.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
            }
        }

        public static void SaveCategories(List<TreasurerCategory> list)
        {
            lock (_categoriesLock)
            {
                SaveList(CategoriesPath, list ?? new List<TreasurerCategory>());
            }
        }

        // --- Audit ---
        public static List<TreasurerAuditEntry> GetAuditEntries(DateTime? from = null, DateTime? to = null, string userName = null, string entityType = null)
        {
            lock (_auditLock)
            {
                var list = LoadList<List<TreasurerAuditEntry>>(AuditPath) ?? new List<TreasurerAuditEntry>();
                if (from.HasValue) list = list.Where(a => a.Timestamp >= from.Value).ToList();
                if (to.HasValue) list = list.Where(a => a.Timestamp <= to.Value).ToList();
                if (!string.IsNullOrWhiteSpace(userName)) list = list.Where(a => string.Equals(a.UserName, userName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (!string.IsNullOrWhiteSpace(entityType)) list = list.Where(a => string.Equals(a.EntityType, entityType, StringComparison.OrdinalIgnoreCase)).ToList();
                return list.OrderByDescending(a => a.Timestamp).ToList();
            }
        }

        public static void AppendAudit(string userName, string action, string entityType, string entityId, string oldValue, string newValue)
        {
            var entry = new TreasurerAuditEntry
            {
                Timestamp = DateTime.Now,
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue ?? "",
                NewValue = newValue ?? ""
            };
            lock (_auditLock)
            {
                var list = LoadList<List<TreasurerAuditEntry>>(AuditPath) ?? new List<TreasurerAuditEntry>();
                list.Add(entry);
                SaveList(AuditPath, list);
            }
        }

        // --- Summaries for dashboard/reports ---
        public static void GetPeriodTotals(DateTime from, DateTime to, out decimal totalIncome, out decimal totalExpense, out decimal netMovement)
        {
            var txList = GetTransactions(from, to);
            totalIncome = txList.Where(t => !t.IsVoided && t.Type == (int)TreasurerTransactionType.Income).Sum(t => t.Amount);
            totalExpense = txList.Where(t => !t.IsVoided && t.Type == (int)TreasurerTransactionType.Expense).Sum(t => t.Amount);
            // Transfers and adjustments: adjustment affects one account; we don't add to income/expense for net movement. Net = income - expense.
            netMovement = totalIncome - totalExpense;
        }

        public static int CountUncategorisedTransactions(DateTime? from = null, DateTime? to = null)
        {
            var list = GetTransactions(from, to).Where(t => !t.IsVoided && t.Type != (int)TreasurerTransactionType.Transfer && !t.CategoryId.HasValue).ToList();
            return list.Count;
        }

        /// <summary>Transactions that are Income or Expense and have no category assigned (excludes Transfer).</summary>
        public static List<TreasurerTransaction> GetUncategorisedTransactions()
        {
            return GetTransactions()
                .Where(t => !t.IsVoided && t.Type != (int)TreasurerTransactionType.Transfer && !t.CategoryId.HasValue)
                .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)
                .ToList();
        }

        /// <summary>True if a non-voided transaction already exists with the same date (date part), amount, and description (trimmed).</summary>
        public static bool ExistsTransactionWithSameDateAmountDescription(DateTime date, decimal amount, string description)
        {
            var desc = (description ?? "").Trim();
            lock (_transactionsLock)
            {
                var list = LoadList<List<TreasurerTransaction>>(TransactionsPath) ?? new List<TreasurerTransaction>();
                return list.Any(t => !t.IsVoided && t.Date.Date == date.Date && t.Amount == amount && (t.Description ?? "").Trim().Equals(desc, StringComparison.OrdinalIgnoreCase));
            }
        }

        // --- Bank import rules ---
        public static List<BankImportRule> GetBankImportRules()
        {
            lock (_bankRulesLock)
            {
                return LoadList<List<BankImportRule>>(BankImportRulesPath) ?? new List<BankImportRule>();
            }
        }

        public static void SaveBankImportRules(List<BankImportRule> list)
        {
            lock (_bankRulesLock)
            {
                SaveList(BankImportRulesPath, list ?? new List<BankImportRule>());
            }
        }

        /// <summary>Apply bank import rules: first matching pattern (case-insensitive contains) returns that category id; otherwise null.</summary>
        public static Guid? ResolveCategoryForBankDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return null;
            var desc = description.Trim().ToUpperInvariant();
            var rules = GetBankImportRules().OrderBy(r => r.SortOrder).ToList();
            foreach (var r in rules)
            {
                if (string.IsNullOrWhiteSpace(r.Pattern)) continue;
                if (desc.Contains(r.Pattern.Trim().ToUpperInvariant()))
                    return r.CategoryId;
            }
            return null;
        }

        // --- Bank reconciliation: batches and bank transactions ---
        public static List<TreasurerBankImportBatch> GetBankBatches(Guid? accountId = null)
        {
            lock (_bankBatchesLock)
            {
                var list = LoadList<List<TreasurerBankImportBatch>>(BankBatchesPath) ?? new List<TreasurerBankImportBatch>();
                if (accountId.HasValue) list = list.Where(b => b.AccountId == accountId.Value).ToList();
                return list.OrderByDescending(b => b.ImportedAt).ToList();
            }
        }

        public static void SaveBankBatches(List<TreasurerBankImportBatch> list)
        {
            lock (_bankBatchesLock) { SaveList(BankBatchesPath, list ?? new List<TreasurerBankImportBatch>()); }
        }

        public static List<TreasurerBankTransaction> GetBankTransactions(
            Guid? accountId = null, Guid? batchId = null,
            DateTime? from = null, DateTime? to = null,
            int? matchStatus = null, bool includeIgnored = false)
        {
            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                if (accountId.HasValue) list = list.Where(t => t.AccountId == accountId.Value).ToList();
                if (batchId.HasValue) list = list.Where(t => t.ImportBatchId == batchId.Value).ToList();
                if (from.HasValue) list = list.Where(t => t.TransactionDate.Date >= from.Value.Date).ToList();
                if (to.HasValue) list = list.Where(t => t.TransactionDate.Date <= to.Value.Date).ToList();
                if (matchStatus.HasValue) list = list.Where(t => t.MatchStatus == matchStatus.Value).ToList();
                if (!includeIgnored) list = list.Where(t => !t.IsIgnored).ToList();
                return list.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id).ToList();
            }
        }

        public static void SaveBankTransactions(List<TreasurerBankTransaction> list)
        {
            lock (_bankTransactionsLock) { SaveList(BankTransactionsPath, list ?? new List<TreasurerBankTransaction>()); }
        }

        /// <summary>True if a bank transaction already exists with same account, date, amount, description (trimmed).</summary>
        public static bool ExistsBankTransactionDuplicate(Guid accountId, DateTime date, decimal amount, string description)
        {
            var desc = (description ?? "").Trim();
            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                return list.Any(t => t.AccountId == accountId && t.TransactionDate.Date == date.Date && t.Amount == amount && (t.Description ?? "").Trim().Equals(desc, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>Suggest internal transactions that could match this bank transaction. Returns ordered by confidence (desc).</summary>
        public static List<(TreasurerTransaction tx, int confidence)> SuggestMatchesForBankTransaction(TreasurerBankTransaction bankTx)
        {
            var amount = Math.Abs(bankTx.Amount);
            var isCredit = bankTx.Amount > 0;
            var internalType = isCredit ? (int)TreasurerTransactionType.Income : (int)TreasurerTransactionType.Expense;
            var candidates = GetTransactions(accountId: bankTx.AccountId, type: internalType)
                .Where(t => !t.IsVoided && t.Amount == amount).ToList();
            var results = new List<(TreasurerTransaction, int)>();
            var descBank = (bankTx.Description ?? "").Trim().ToUpperInvariant();
            foreach (var t in candidates)
            {
                var dateDiff = Math.Abs((t.Date.Date - bankTx.TransactionDate.Date).TotalDays);
                int score = 0;
                if (dateDiff == 0) score += 50;
                else if (dateDiff <= 1) score += 40;
                else if (dateDiff <= 3) score += 25;
                else if (dateDiff <= 7) score += 10;
                var descInt = (t.Description ?? "").Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(descBank) && !string.IsNullOrEmpty(descInt) && (descBank.Contains(descInt) || descInt.Contains(descBank)))
                    score += 30;
                else if (descBank.Length > 3 && descInt.Length > 3 && LevenshteinSimilarity(descBank, descInt) > 0.5)
                    score += 15;
                if (score > 0) results.Add((t, Math.Min(100, score)));
            }
            return results.OrderByDescending(x => x.Item2).ToList();
        }

        private static double LevenshteinSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
            int lenA = a.Length, lenB = b.Length;
            var d = new int[lenA + 1, lenB + 1];
            for (int i = 0; i <= lenA; i++) d[i, 0] = i;
            for (int j = 0; j <= lenB; j++) d[0, j] = j;
            for (int i = 1; i <= lenA; i++)
                for (int j = 1; j <= lenB; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            int maxLen = Math.Max(lenA, lenB);
            return 1.0 - (double)d[lenA, lenB] / maxLen;
        }

        public static void ConfirmBankTransactionMatch(Guid bankTxId, Guid internalTxId, string confirmedBy)
        {
            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                var bt = list.FirstOrDefault(t => t.Id == bankTxId);
                if (bt == null) return;
                var oldStatus = bt.MatchStatus.ToString();
                bt.MatchedTransactionId = internalTxId;
                bt.MatchStatus = (int)BankTransactionMatchStatus.ManuallyMatched;
                bt.MatchedBy = confirmedBy;
                bt.MatchedAt = DateTime.Now;
                SaveList(BankTransactionsPath, list);
                AppendReconciliationAudit(bankTxId, internalTxId, confirmedBy, "Matched", oldStatus, "ManuallyMatched");
            }
        }

        public static void UnmatchBankTransaction(Guid bankTxId, string userName)
        {
            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                var bt = list.FirstOrDefault(t => t.Id == bankTxId);
                if (bt == null) return;
                var oldVal = bt.MatchedTransactionId?.ToString() ?? "";
                bt.MatchedTransactionId = null;
                bt.SuggestedMatchTransactionId = null;
                bt.MatchConfidence = 0;
                bt.MatchStatus = (int)BankTransactionMatchStatus.Unmatched;
                bt.MatchedBy = "";
                bt.MatchedAt = null;
                bt.IsReconciled = false;
                bt.ReconciledBy = "";
                bt.ReconciledAt = null;
                SaveList(BankTransactionsPath, list);
                AppendReconciliationAudit(bankTxId, null, userName, "Unmatched", oldVal, "");
            }
        }

        public static void MarkBankTransactionReconciled(Guid bankTxId, string reconciledBy)
        {
            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                var bt = list.FirstOrDefault(t => t.Id == bankTxId);
                if (bt == null) return;
                bt.IsReconciled = true;
                bt.ReconciledBy = reconciledBy;
                bt.ReconciledAt = DateTime.Now;
                if (bt.MatchStatus != (int)BankTransactionMatchStatus.Reconciled)
                {
                    var oldStatus = bt.MatchStatus.ToString();
                    bt.MatchStatus = (int)BankTransactionMatchStatus.Reconciled;
                    SaveList(BankTransactionsPath, list);
                    AppendReconciliationAudit(bt.Id, bt.MatchedTransactionId, reconciledBy, "Reconciled", oldStatus, "Reconciled");
                }
                else SaveList(BankTransactionsPath, list);
            }
        }

        /// <summary>Create an internal transaction from this bank transaction and link it. Used for bank-only items (fees, interest, etc.).</summary>
        public static TreasurerTransaction CreateInternalTransactionFromBankTransaction(Guid bankTxId, string enteredBy)
        {
            TreasurerBankTransaction bt = null;
            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                bt = list.FirstOrDefault(t => t.Id == bankTxId);
                if (bt == null) return null;
            }
            var amount = Math.Abs(bt.Amount);
            var type = bt.Amount > 0 ? (int)TreasurerTransactionType.Income : (int)TreasurerTransactionType.Expense;
            var internalTx = new TreasurerTransaction
            {
                Date = bt.TransactionDate,
                Type = type,
                Amount = amount,
                Description = bt.Description ?? "",
                AccountId = bt.AccountId,
                CategoryId = bt.SuggestedCategoryId,
                Source = BankStatementImporter.SourceBankImport,
                Reference = "Created from bank reconciliation"
            };
            AddTransaction(internalTx, enteredBy);

            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                var b = list.FirstOrDefault(t => t.Id == bankTxId);
                if (b != null)
                {
                    b.CreatedInternalTransactionId = internalTx.Id;
                    b.MatchedTransactionId = internalTx.Id;
                    b.MatchStatus = (int)BankTransactionMatchStatus.ManuallyMatched;
                    b.MatchedBy = enteredBy;
                    b.MatchedAt = DateTime.Now;
                    SaveList(BankTransactionsPath, list);
                    AppendReconciliationAudit(bankTxId, internalTx.Id, enteredBy, "CreatedInternal", "", internalTx.Id.ToString());
                }
            }
            return internalTx;
        }

        public static void IgnoreBankTransaction(Guid bankTxId, string userName)
        {
            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                var bt = list.FirstOrDefault(t => t.Id == bankTxId);
                if (bt == null) return;
                bt.IsIgnored = true;
                bt.IgnoredBy = userName;
                bt.IgnoredAt = DateTime.Now;
                SaveList(BankTransactionsPath, list);
                AppendReconciliationAudit(bankTxId, null, userName, "Ignored", "", "");
            }
        }

        public static void UnignoreBankTransaction(Guid bankTxId, string userName)
        {
            lock (_bankTransactionsLock)
            {
                var list = LoadList<List<TreasurerBankTransaction>>(BankTransactionsPath) ?? new List<TreasurerBankTransaction>();
                var bt = list.FirstOrDefault(t => t.Id == bankTxId);
                if (bt == null) return;
                bt.IsIgnored = false;
                bt.IgnoredBy = "";
                bt.IgnoredAt = null;
                SaveList(BankTransactionsPath, list);
                AppendReconciliationAudit(bankTxId, null, userName, "Unignored", "", "");
            }
        }

        public static void AppendReconciliationAudit(Guid bankTxId, Guid? internalTxId, string userName, string action, string oldValue, string newValue)
        {
            var entry = new TreasurerReconciliationAuditEntry
            {
                Timestamp = DateTime.Now,
                UserName = userName,
                Action = action,
                BankTransactionId = bankTxId,
                InternalTransactionId = internalTxId,
                OldValue = oldValue ?? "",
                NewValue = newValue ?? ""
            };
            lock (_reconciliationAuditLock)
            {
                var list = LoadList<List<TreasurerReconciliationAuditEntry>>(ReconciliationAuditPath) ?? new List<TreasurerReconciliationAuditEntry>();
                list.Add(entry);
                SaveList(ReconciliationAuditPath, list);
            }
        }

        public static List<TreasurerReconciliationAuditEntry> GetReconciliationAuditEntries(Guid? bankTxId = null, int? limit = 200)
        {
            lock (_reconciliationAuditLock)
            {
                var list = LoadList<List<TreasurerReconciliationAuditEntry>>(ReconciliationAuditPath) ?? new List<TreasurerReconciliationAuditEntry>();
                if (bankTxId.HasValue) list = list.Where(a => a.BankTransactionId == bankTxId.Value).ToList();
                return list.OrderByDescending(a => a.Timestamp).Take(limit ?? 200).ToList();
            }
        }

        // --- Persistence helpers ---
        private static T LoadList<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var ser = new DataContractJsonSerializer(typeof(T));
                    return (T)ser.ReadObject(fs);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TreasurerService Load {path}: {ex.Message}");
                return null;
            }
        }

        private static void SaveList<T>(string path, T data) where T : class
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var ser = new DataContractJsonSerializer(typeof(T));
                    ser.WriteObject(fs, data);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TreasurerService Save {path}: {ex.Message}");
                throw;
            }
        }
    }
}


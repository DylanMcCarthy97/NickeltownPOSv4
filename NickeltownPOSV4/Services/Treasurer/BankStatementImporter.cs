using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NickeltownPOSV4.Services.Treasurer
{
    /// <summary>Parses ANZ-style bank CSV exports and imports them as treasurer transactions with duplicate detection and rule-based categorisation.</summary>
    public static class BankStatementImporter
    {
        public const string SourceBankImport = "BankImport";

        /// <summary>One row from a bank CSV (before type resolution).</summary>
        public class BankCsvRow
        {
            public DateTime Date { get; set; }
            public decimal Amount { get; set; }  // Positive = credit/income, negative = debit/expense
            public string Description { get; set; } = string.Empty;
            public decimal? Balance { get; set; }  // Statement running balance if column present
        }

        /// <summary>Result of an import run (direct to internal transactions).</summary>
        public class ImportResult
        {
            public int Imported { get; set; }
            public int SkippedDuplicate { get; set; }
            public int SkippedInvalid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        /// <summary>Result of importing CSV into bank transactions for reconciliation.</summary>
        public class BankImportToReconciliationResult
        {
            public Guid BatchId { get; set; }
            public int Imported { get; set; }
            public int SkippedDuplicate { get; set; }
            public int SkippedInvalid { get; set; }
            public int AutoSuggestedMatchCount { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        /// <summary>Result of parsing a CSV file: rows plus skip stats for preview.</summary>
        public class ParseCsvResult
        {
            public List<BankCsvRow> Rows { get; set; } = new List<BankCsvRow>();
            public int SkippedCount { get; set; }
            public List<string> SkipReasons { get; set; } = new List<string>();
            public bool HadHeaderRow { get; set; }
        }

        /// <summary>Parse an ANZ-style CSV. Supports: (1) Headerless 3-column: Date, Amount (signed), Description; optional 4th Balance. (2) Header row with named columns. Trims whitespace, ignores blank rows.</summary>
        public static ParseCsvResult ParseCsvWithPreview(string filePath)
        {
            var result = new ParseCsvResult();
            if (!File.Exists(filePath)) return result;

            var lines = File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
            if (lines.Count == 0) return result;

            var firstCells = ParseCsvLine(lines[0]).Select(c => (c ?? "").Trim()).ToList();
            bool hasHeader = firstCells.Count >= 3 && (
                firstCells[0].ToLowerInvariant().Contains("date") ||
                (firstCells.Count > 2 && firstCells[2].ToLowerInvariant().Contains("description")) ||
                (firstCells.Count > 2 && firstCells[2].ToLowerInvariant().Contains("details")));
            int startIndex = hasHeader ? 1 : 0;
            result.HadHeaderRow = hasHeader;

            for (int i = startIndex; i < lines.Count; i++)
            {
                var cells = ParseCsvLine(lines[i]).Select(c => (c ?? "").Trim()).ToList();
                int rowNum = i + 1;

                if (cells.Count < 3)
                {
                    result.SkippedCount++;
                    result.SkipReasons.Add($"Row {rowNum}: Not enough columns (need at least 3)");
                    continue;
                }

                if (!TryParseDate(cells[0], out DateTime date))
                {
                    result.SkippedCount++;
                    result.SkipReasons.Add($"Row {rowNum}: Invalid date '{cells[0]}'");
                    continue;
                }

                if (!TryParseDecimal(cells[1], out decimal amount))
                {
                    result.SkippedCount++;
                    result.SkipReasons.Add($"Row {rowNum}: Invalid amount '{cells[1]}'");
                    continue;
                }

                string description = cells[2].Trim();
                decimal? balance = null;
                if (cells.Count >= 4 && TryParseDecimal(cells[3], out decimal b))
                    balance = b;

                result.Rows.Add(new BankCsvRow { Date = date, Amount = amount, Description = description, Balance = balance });
            }

            return result;
        }

        private static bool TryParseDecimal(string value, out decimal amount)
        {
            amount = 0;
            value = (value ?? "").Replace("$", "").Replace(",", "").Trim();
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        }

        /// <summary>Parse CSV and return only the rows (for backward compatibility). Uses same logic as ParseCsvWithPreview.</summary>
        public static List<BankCsvRow> ParseCsv(string filePath)
        {
            return ParseCsvWithPreview(filePath).Rows;
        }

        /// <summary>Legacy: Parse with header-based column detection. Used when file has named headers.</summary>
        private static List<BankCsvRow> ParseCsvWithHeader(string filePath, List<string> header)
        {
            var rows = new List<BankCsvRow>();
            if (!File.Exists(filePath)) return rows;

            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) return rows;

            var colIndex = GetColumnIndices(header.Select(x => (x ?? "").Trim().ToLowerInvariant()).ToList());
            for (int i = 1; i < lines.Length; i++)
            {
                var cells = ParseCsvLine(lines[i]).Select(c => (c ?? "").Trim()).ToList();
                if (cells.Count == 0) continue;

                if (!TryParseDate(GetCell(cells, colIndex.DateIndex), out DateTime date)) continue;
                string description = GetCell(cells, colIndex.DescIndex).Trim();
                if (!TryParseAmount(cells, colIndex, out decimal amount)) continue;

                decimal? balance = null;
                if (colIndex.BalanceIndex >= 0 && colIndex.BalanceIndex < cells.Count && TryParseDecimal(GetCell(cells, colIndex.BalanceIndex), out decimal b))
                    balance = b;
                rows.Add(new BankCsvRow { Date = date, Amount = amount, Description = description, Balance = balance });
            }
            return rows;
        }

        /// <summary>Import parsed CSV rows into bank transactions (reconciliation workflow). Creates a batch, stores bank transactions with duplicate detection, auto-suggests category and internal matches.</summary>
        public static BankImportToReconciliationResult ImportToBankTransactions(List<BankCsvRow> rows, Guid accountId, string fileName, string enteredBy)
        {
            var result = new BankImportToReconciliationResult();
            if (rows == null || rows.Count == 0) return result;

            var batch = new TreasurerBankImportBatch
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                FileName = fileName ?? "import.csv",
                ImportedBy = enteredBy,
                ImportedAt = DateTime.Now,
                StatementStartDate = rows.Min(r => r.Date),
                StatementEndDate = rows.Max(r => r.Date)
            };
            result.BatchId = batch.Id;

            var bankTxList = new List<TreasurerBankTransaction>();
            var existingBatches = TreasurerService.GetBankBatches(null);
            var existingBankTx = TreasurerService.GetBankTransactions(accountId: accountId, includeIgnored: true);

            foreach (var row in rows)
            {
                if (row.Amount == 0)
                {
                    result.SkippedInvalid++;
                    continue;
                }
                string desc = (row.Description ?? "").Trim();
                if (TreasurerService.ExistsBankTransactionDuplicate(accountId, row.Date, row.Amount, desc))
                {
                    result.SkippedDuplicate++;
                    continue;
                }
                var suggestedCategoryId = TreasurerService.ResolveCategoryForBankDescription(desc);
                var bt = new TreasurerBankTransaction
                {
                    ImportBatchId = batch.Id,
                    AccountId = accountId,
                    TransactionDate = row.Date,
                    Description = desc,
                    Amount = row.Amount,
                    Balance = row.Balance,
                    SuggestedCategoryId = suggestedCategoryId,
                    MatchStatus = (int)BankTransactionMatchStatus.Unmatched
                };
                bankTxList.Add(bt);
                result.Imported++;
            }

            if (bankTxList.Count > 0)
            {
                var allBatches = TreasurerService.GetBankBatches(null);
                allBatches.Insert(0, batch);
                TreasurerService.SaveBankBatches(allBatches);

                var allBankTx = TreasurerService.GetBankTransactions(includeIgnored: true);
                const int AutoSuggestThreshold = 60;
                foreach (var bt in bankTxList)
                {
                    var suggestions = TreasurerService.SuggestMatchesForBankTransaction(bt);
                    if (suggestions.Count > 0 && suggestions[0].Item2 >= AutoSuggestThreshold)
                    {
                        bt.SuggestedMatchTransactionId = suggestions[0].Item1.Id;
                        bt.MatchConfidence = suggestions[0].Item2;
                        bt.MatchStatus = (int)BankTransactionMatchStatus.AutoSuggested;
                        result.AutoSuggestedMatchCount++;
                    }
                    allBankTx.Insert(0, bt);
                }
                TreasurerService.SaveBankTransactions(allBankTx);
            }

            return result;
        }

        /// <summary>Import parsed rows into treasurer transactions: duplicate check, rule-based category, then AddTransaction.</summary>
        public static ImportResult ImportRows(List<BankCsvRow> rows, Guid accountId, string enteredBy)
        {
            var result = new ImportResult();
            var categories = TreasurerService.GetCategories().ToList();

            foreach (var row in rows)
            {
                if (row.Amount == 0)
                {
                    result.SkippedInvalid++;
                    continue;
                }

                string desc = (row.Description ?? "").Trim();
                if (TreasurerService.ExistsTransactionWithSameDateAmountDescription(row.Date, row.Amount, desc))
                {
                    result.SkippedDuplicate++;
                    continue;
                }

                int type = row.Amount > 0 ? (int)TreasurerTransactionType.Income : (int)TreasurerTransactionType.Expense;
                decimal amount = Math.Abs(row.Amount);

                var categoryId = TreasurerService.ResolveCategoryForBankDescription(desc);

                var t = new TreasurerTransaction
                {
                    Date = row.Date,
                    Type = type,
                    Amount = amount,
                    Description = desc,
                    AccountId = accountId,
                    CategoryId = categoryId,
                    Source = SourceBankImport,
                    Reference = "Bank import"
                };
                try
                {
                    TreasurerService.AddTransaction(t, enteredBy);
                    result.Imported++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{row.Date:yyyy-MM-dd} {amount:N2}: {ex.Message}");
                }
            }

            return result;
        }

        private struct ColumnIndices
        {
            public int DateIndex;
            public int AmountIndex;
            public int DebitIndex;
            public int CreditIndex;
            public int DescIndex;
            public int BalanceIndex;
        }

        private static ColumnIndices GetColumnIndices(List<string> header)
        {
            var h = header.Select(x => (x ?? "").Trim().ToLowerInvariant()).ToList();
            int dateIdx = -1, amountIdx = -1, debitIdx = -1, creditIdx = -1, descIdx = -1, balanceIdx = -1;

            for (int i = 0; i < h.Count; i++)
            {
                var c = h[i];
                if (c.Contains("date")) dateIdx = i;
                else if (c == "amount" || (c.TrimStart().StartsWith("amount") && !c.Contains("debit") && !c.Contains("credit"))) amountIdx = i;  // "Amount", "Amount (AUD)", etc.
                else if (c.Contains("debit")) debitIdx = i;
                else if (c.Contains("credit")) creditIdx = i;
                else if (c.Contains("description") || c.Contains("details") || c.Contains("narrative") || c.Contains("transaction details")) descIdx = i;
                else if (c.Contains("balance")) balanceIdx = i;
            }

            if (dateIdx < 0) dateIdx = 0;
            if (descIdx < 0) descIdx = 1;

            return new ColumnIndices
            {
                DateIndex = dateIdx,
                AmountIndex = amountIdx,
                DebitIndex = debitIdx,
                CreditIndex = creditIdx,
                DescIndex = descIdx >= 0 ? descIdx : (header.Count > 2 ? 1 : 0),
                BalanceIndex = balanceIdx
            };
        }

        private static List<string> ParseCsvLine(string line)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(line)) return list;
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes && (c == ',' || c == '\t'))
                {
                    list.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            list.Add(current.ToString().Trim());
            return list;
        }

        private static string GetCell(List<string> cells, int index)
        {
            if (index < 0 || index >= cells.Count) return "";
            return cells[index] ?? "";
        }

        private static bool TryParseDate(string value, out DateTime date)
        {
            date = default;
            value = (value ?? "").Trim();
            if (string.IsNullOrEmpty(value)) return false;

            string[] formats = { "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d/M/yy", "dd/MM/yy", "d MMM yyyy", "dd MMM yyyy" };
            foreach (var f in formats)
            {
                if (DateTime.TryParseExact(value, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    return true;
            }
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private static bool TryParseAmount(List<string> cells, ColumnIndices col, out decimal amount)
        {
            amount = 0;

            if (col.AmountIndex >= 0 && col.AmountIndex < cells.Count)
            {
                var v = GetCell(cells, col.AmountIndex).Replace("$", "").Replace(",", "").Trim();
                if (decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                    return true;
            }

            decimal debit = 0, credit = 0;
            if (col.DebitIndex >= 0 && col.DebitIndex < cells.Count)
            {
                var v = GetCell(cells, col.DebitIndex).Replace("$", "").Replace(",", "").Trim();
                decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out debit);
            }
            if (col.CreditIndex >= 0 && col.CreditIndex < cells.Count)
            {
                var v = GetCell(cells, col.CreditIndex).Replace("$", "").Replace(",", "").Trim();
                decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out credit);
            }

            if (debit != 0 || credit != 0)
            {
                amount = credit - debit;
                return true;
            }

            return false;
        }
    }
}


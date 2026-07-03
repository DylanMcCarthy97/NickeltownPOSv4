using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Treasurer;

/// <summary>Generates treasurer PDF reports (ported from POSBar V2).</summary>
public static class TreasurerReportGenerator
{
    private const string ClubName = "Nickeltown Flounderers Inc";
    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("en-AU");

    public static byte[] BuildSummaryPdf(string? preparedBy = null)
    {
        var year = DateTime.Today.Year;
        var quarter = (DateTime.Today.Month - 1) / 3 + 1;
        var qStart = new DateTime(year, (quarter - 1) * 3 + 1, 1);
        var qEnd = qStart.AddMonths(3).AddDays(-1);
        using var ms = new MemoryStream();
        WriteQuarterlyPdf(ms, qStart, qEnd, preparedBy ?? "Treasurer");
        return ms.ToArray();
    }

    private static void WriteQuarterlyPdf(Stream output, DateTime qStart, DateTime qEnd, string preparedBy)
    {
        var accounts = TreasurerService.GetAccounts().OrderBy(a => a.SortOrder).ThenBy(a => a.Name).Where(a => a.IsActive).ToList();
        TreasurerService.RefreshComputedBalances();
        var txList = TreasurerService.GetTransactions(qStart, qEnd);
        var incomeTx = txList.Where(t => !t.IsVoided && t.Type == (int)TreasurerTransactionType.Income).OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ToList();
        var expenseTx = txList.Where(t => !t.IsVoided && t.Type == (int)TreasurerTransactionType.Expense).OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ToList();
        var totalIncome = incomeTx.Sum(t => t.Amount);
        var totalExpense = expenseTx.Sum(t => t.Amount);

        decimal openingBalance = 0;
        decimal closingBalance = 0;
        foreach (var a in accounts)
        {
            openingBalance += GetOpeningBalance(a.Id, qStart);
            closingBalance += TreasurerService.GetAccountBalance(a.Id);
        }

        using var doc = new Document(PageSize.A4, 36, 36, 36, 36);
        PdfWriter.GetInstance(doc, output);
        doc.Open();

        var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, new BaseColor(64, 64, 64));
        var fontSubtitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(64, 64, 64));
        var fontStatement = FontFactory.GetFont(FontFactory.HELVETICA, 11, new BaseColor(64, 64, 64));
        var fontPeriod = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(64, 64, 64));
        var fontSection = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(64, 64, 64));
        var fontBody = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(64, 64, 64));
        var fontBodyBold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(64, 64, 64));
        var fontClosing = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(64, 64, 64));

        AddLogoBanner(doc);
        doc.Add(new Paragraph("Treasurer's Report", fontTitle) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 6 });
        doc.Add(new Paragraph(ClubName, fontSubtitle) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 4 });
        doc.Add(new Paragraph("Income & Expense Statement", fontStatement) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 16 });
        doc.Add(new Paragraph("Reporting Period", fontBody) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 2 });
        doc.Add(new Paragraph($"{qStart:d MMMM yyyy} – {qEnd:d MMMM yyyy}", fontPeriod) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 24 });

        var tblSummary = new PdfPTable(2) { WidthPercentage = 70, HorizontalAlignment = Element.ALIGN_CENTER, SpacingAfter = 20 };
        tblSummary.SetWidths(new float[] { 3, 1 });
        tblSummary.DefaultCell.BorderWidth = 0;
        tblSummary.DefaultCell.Padding = 4;
        AddSummaryRow(tblSummary, "Opening Balance", openingBalance, fontBody);
        AddSummaryRow(tblSummary, "Total Income", totalIncome, fontBody);
        AddSummaryRow(tblSummary, "Total Expenses", totalExpense, fontBody);
        AddSummaryRow(tblSummary, "Closing Balance", closingBalance, fontBodyBold);
        doc.Add(tblSummary);

        doc.Add(new Paragraph("Income", fontSection) { SpacingBefore = 8, SpacingAfter = 8 });
        AddTransactionTable(doc, incomeTx, "Total Income", totalIncome, fontBody, fontBodyBold);

        doc.Add(new Paragraph("Expenses", fontSection) { SpacingBefore = 16, SpacingAfter = 8 });
        AddTransactionTable(doc, expenseTx, "Total Expenses", totalExpense, fontBody, fontBodyBold);

        doc.Add(new Paragraph("Prepared By", fontSection) { SpacingBefore = 16, SpacingAfter = 4 });
        doc.Add(new Paragraph(preparedBy, fontBody));
        doc.Close();
    }

    private static string FormatCurrency(decimal amount) => amount.ToString("C2", CurrencyCulture);

    private static void AddSummaryRow(PdfPTable table, string label, decimal amount, Font font)
    {
        table.AddCell(CellLeft(label, font));
        table.AddCell(CellRight(FormatCurrency(amount), font));
    }

    private static void AddTransactionTable(
        Document doc,
        List<TreasurerTransaction> transactions,
        string totalLabel,
        decimal totalAmount,
        Font fontBody,
        Font fontBold)
    {
        var table = new PdfPTable(3) { WidthPercentage = 100, SpacingAfter = 0 };
        table.SetWidths(new float[] { 22, 58, 20 });
        table.HeaderRows = 1;
        table.AddCell(CellLeft("Date", fontBold));
        table.AddCell(CellLeft("Description", fontBold));
        table.AddCell(CellRight("Amount", fontBold));
        foreach (var t in transactions)
        {
            table.AddCell(CellLeft(t.Date.ToString("dd MMM yyyy", CurrencyCulture), fontBody));
            table.AddCell(CellLeft(string.IsNullOrWhiteSpace(t.Description) ? "—" : t.Description, fontBody));
            table.AddCell(CellRight(FormatCurrency(t.Amount), fontBody));
        }

        table.AddCell(CellLeft(totalLabel, fontBold));
        table.AddCell(CellLeft(string.Empty, fontBold));
        table.AddCell(CellRight(FormatCurrency(totalAmount), fontBold));
        doc.Add(table);
    }

    private static PdfPCell CellLeft(string text, Font font)
    {
        var c = new PdfPCell(new Phrase(text ?? string.Empty, font))
        {
            BorderWidth = 0.5f,
            BorderColor = new BaseColor(211, 211, 211),
            HorizontalAlignment = Element.ALIGN_LEFT,
            Padding = 5,
        };
        return c;
    }

    private static PdfPCell CellRight(string text, Font font)
    {
        var c = new PdfPCell(new Phrase(text ?? string.Empty, font))
        {
            BorderWidth = 0.5f,
            BorderColor = new BaseColor(211, 211, 211),
            HorizontalAlignment = Element.ALIGN_RIGHT,
            Padding = 5,
        };
        return c;
    }

    private static decimal GetOpeningBalance(Guid accountId, DateTime beforeDate)
    {
        var acc = TreasurerService.GetAccounts().FirstOrDefault(a => a.Id == accountId);
        if (acc is null)
        {
            return 0;
        }

        var balance = acc.OpeningBalance;
        var allTx = TreasurerService.GetTransactions();
        foreach (var t in allTx.Where(x => !x.IsVoided && x.Date < beforeDate).OrderBy(x => x.Date).ThenBy(x => x.CreatedAt))
        {
            if (t.Type == (int)TreasurerTransactionType.Transfer)
            {
                if (t.AccountId == accountId)
                {
                    balance -= t.Amount;
                }

                if (t.ToAccountId == accountId)
                {
                    balance += t.Amount;
                }
            }
            else if (t.Type == (int)TreasurerTransactionType.Income || t.Type == (int)TreasurerTransactionType.Adjustment)
            {
                if (t.AccountId == accountId)
                {
                    balance += t.Amount;
                }
            }
            else if (t.Type == (int)TreasurerTransactionType.Expense)
            {
                if (t.AccountId == accountId)
                {
                    balance -= t.Amount;
                }
            }
        }

        return balance;
    }

    private static void AddLogoBanner(Document doc)
    {
        var logoCell = ReportPdfBranding.CreateLogoCell(72f, 72f);
        logoCell.HorizontalAlignment = Element.ALIGN_CENTER;
        var row = new PdfPTable(1) { WidthPercentage = 30, HorizontalAlignment = Element.ALIGN_CENTER, SpacingAfter = 8 };
        row.AddCell(logoCell);
        doc.Add(row);
    }
}

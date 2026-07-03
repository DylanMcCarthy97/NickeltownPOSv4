using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Dapper;
using iTextSharp.text;
using iTextSharp.text.pdf;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using PdfFont = iTextSharp.text.Font;

namespace NickeltownPOSV4.Services.Settings;

/// <summary>
/// Monthly bar-tabs PDF (A4). Modernised layout: brand-blue header, five summary cards
/// (Members / Owing / In Credit / Net Position / Avg Balance), a modern member-balance
/// table with status pills, right-aligned currency, and zebra striping. Mirrors V2's
/// information density while using a cleaner, more branded look.
/// </summary>
internal static class MonthlyTabsPdfBuilder
{
    private const string ClubName = "Nickeltown Flounderers";

    private const string ReportTitle = "Monthly Bar Tabs";

    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("en-AU");

    // ----- Brand palette ----------------------------------------------------
    private static readonly BaseColor BrandPrimary = new(30, 58, 138);     // #1E3A8A
    private static readonly BaseColor BrandAccent = new(59, 130, 246);     // #3B82F6
    private static readonly BaseColor BrandSoft = new(238, 242, 255);      // #EEF2FF
    private static readonly BaseColor Owing = new(185, 28, 28);            // #B91C1C
    private static readonly BaseColor OwingSoft = new(254, 226, 226);      // #FEE2E2
    private static readonly BaseColor Credit = new(21, 128, 61);           // #15803D
    private static readonly BaseColor CreditSoft = new(220, 252, 231);     // #DCFCE7
    private static readonly BaseColor Neutral = new(75, 85, 99);           // #4B5563
    private static readonly BaseColor NeutralSoft = new(243, 244, 246);    // #F3F4F6
    private static readonly BaseColor Muted = new(107, 114, 128);          // #6B7280
    private static readonly BaseColor TextPrimary = new(17, 24, 39);       // #111827
    private static readonly BaseColor BorderColor = new(229, 231, 235);    // #E5E7EB
    private static readonly BaseColor ZebraRow = new(249, 250, 251);       // #F9FAFB
    private static readonly BaseColor White = new(255, 255, 255);

    public static byte[] Build(SqliteConnectionFactory factory, DateTime month)
    {
        var monthStart = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var tabs = LoadTabs(factory, monthStart, monthEnd);

        using var ms = new MemoryStream();
        using (var doc = new Document(PageSize.A4, 30, 30, 32, 32))
        {
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            AddHeader(doc, monthStart);

            if (tabs.Count == 0)
            {
                doc.Add(new Paragraph(
                    "No tab data found for the selected period.",
                    FontFactory.GetFont(FontFactory.HELVETICA, 12, Muted))
                {
                    SpacingBefore = 20,
                });
                return ms.ToArray();
            }

            var owingTabs = tabs.Where(t => t.Balance < 0m).OrderBy(t => t.Balance).ThenBy(t => t.Name).ToList();
            var creditTabs = tabs.Where(t => t.Balance > 0m).OrderByDescending(t => t.Balance).ThenBy(t => t.Name).ToList();
            var settledTabs = tabs.Where(t => t.Balance == 0m).OrderBy(t => t.Name).ToList();

            var totalMembers = tabs.Count;
            var totalOwing = owingTabs.Sum(t => t.Balance);
            var totalCredit = creditTabs.Sum(t => t.Balance);
            var netPosition = tabs.Sum(t => t.Balance);
            var avgBalance = totalMembers == 0 ? 0m : netPosition / totalMembers;
            var withCurrentMonthActivity = tabs.Count(t => t.HadCurrentMonthActivity);

            AddSummaryCards(doc, totalMembers, owingTabs.Count, totalOwing,
                creditTabs.Count, totalCredit, netPosition, avgBalance);

            AddSectionHeader(doc, "MEMBER BALANCES");

            AddBalanceTable(doc, owingTabs, creditTabs, settledTabs, totalOwing, totalCredit);

            AddFooter(doc, totalMembers, withCurrentMonthActivity, creditTabs.Count, owingTabs.Count);
        }

        return ms.ToArray();
    }

    // -------- Header --------------------------------------------------------

    private static void AddHeader(Document doc, DateTime monthStart)
    {
        var header = new PdfPTable(2) { WidthPercentage = 100 };
        header.SetWidths(new float[] { 1.1f, 3f });
        header.SpacingAfter = 4;

        header.AddCell(ReportPdfBranding.CreateLogoCell());

        var titleCell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            Padding = 3,
        };

        var clubFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, BrandAccent);
        titleCell.AddElement(new Paragraph(ClubName.ToUpperInvariant(), clubFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 2,
        });

        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 22, BrandPrimary);
        titleCell.AddElement(new Paragraph(ReportTitle, titleFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 1,
        });

        var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, Muted);
        titleCell.AddElement(new Paragraph(monthStart.ToString("MMMM yyyy", CurrencyCulture), subtitleFont)
        {
            Alignment = Element.ALIGN_RIGHT,
        });

        header.AddCell(titleCell);
        doc.Add(header);

        AddBrandBar(doc);
    }

    private static void AddBrandBar(Document doc)
    {
        var bar = new PdfPTable(1) { WidthPercentage = 100 };
        bar.SpacingBefore = 6;
        bar.SpacingAfter = 10;
        bar.AddCell(new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            BackgroundColor = BrandPrimary,
            FixedHeight = 3f,
        });
        doc.Add(bar);
    }

    // -------- Summary cards -------------------------------------------------

    private static void AddSummaryCards(
        Document doc,
        int totalMembers,
        int owingCount,
        decimal totalOwing,
        int creditCount,
        decimal totalCredit,
        decimal netPosition,
        decimal avgBalance)
    {
        var table = new PdfPTable(5) { WidthPercentage = 100 };
        table.SetWidths(new float[] { 1, 1, 1, 1, 1 });
        table.SpacingAfter = 14;

        AddSummaryCard(table, "MEMBERS",
            totalMembers.ToString(CultureInfo.InvariantCulture),
            null,
            BrandSoft, BrandPrimary, BrandPrimary);

        AddSummaryCard(table, "OWING",
            owingCount.ToString(CultureInfo.InvariantCulture),
            FormatCurrency(totalOwing),
            OwingSoft, Owing, Owing);

        AddSummaryCard(table, "IN CREDIT",
            creditCount.ToString(CultureInfo.InvariantCulture),
            FormatCurrency(totalCredit),
            CreditSoft, Credit, Credit);

        var netColor = netPosition < 0m ? Owing : (netPosition > 0m ? Credit : Neutral);
        var netSoft = netPosition < 0m ? OwingSoft : (netPosition > 0m ? CreditSoft : NeutralSoft);
        AddSummaryCard(table, "NET POSITION",
            FormatCurrency(netPosition),
            null,
            netSoft, netColor, netColor);

        AddSummaryCard(table, "AVG BALANCE",
            FormatCurrency(avgBalance),
            null,
            NeutralSoft, Neutral, Neutral);

        doc.Add(table);
    }

    private static void AddSummaryCard(
        PdfPTable parent,
        string label,
        string value,
        string? secondary,
        BaseColor bg,
        BaseColor accent,
        BaseColor valueColor)
    {
        var inner = new PdfPTable(1) { WidthPercentage = 100 };
        inner.DefaultCell.Border = Rectangle.NO_BORDER;

        var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7.5f, accent);
        var valueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14f, valueColor);
        var secondaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 8.5f, valueColor);

        inner.AddCell(new PdfPCell(new Phrase(label, labelFont))
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_CENTER,
            BackgroundColor = bg,
            PaddingTop = 8,
            PaddingBottom = 2,
        });
        inner.AddCell(new PdfPCell(new Phrase(value, valueFont))
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_CENTER,
            BackgroundColor = bg,
            PaddingTop = 2,
            PaddingBottom = secondary is null ? 8 : 1,
        });

        if (secondary is not null)
        {
            inner.AddCell(new PdfPCell(new Phrase(secondary, secondaryFont))
            {
                Border = Rectangle.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_CENTER,
                BackgroundColor = bg,
                PaddingTop = 0,
                PaddingBottom = 7,
            });
        }

        var outer = new PdfPCell(inner)
        {
            Padding = 0,
            BackgroundColor = bg,
            BorderColor = accent,
            BorderWidthLeft = 3f,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
        };
        parent.AddCell(outer);
    }

    // -------- Section header & table ---------------------------------------

    private static void AddSectionHeader(Document doc, string text)
    {
        var block = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 2, SpacingAfter = 6 };
        var font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10.5f, White);
        block.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = BrandPrimary,
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_LEFT,
            PaddingTop = 6,
            PaddingBottom = 6,
            PaddingLeft = 10,
        });
        doc.Add(block);
    }

    private static void AddBalanceTable(
        Document doc,
        List<TabRow> owingTabs,
        List<TabRow> creditTabs,
        List<TabRow> settledTabs,
        decimal totalOwing,
        decimal totalCredit)
    {
        var table = new PdfPTable(4) { WidthPercentage = 100, SpacingAfter = 6 };
        table.SetWidths(new float[] { 3.9f, 2.3f, 3.4f, 2.4f });
        table.HeaderRows = 1;

        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9.5f, White);
        AddHeaderCell(table, "MEMBER", headerFont, Element.ALIGN_LEFT);
        AddHeaderCell(table, "BALANCE", headerFont, Element.ALIGN_RIGHT);
        AddHeaderCell(table, "STATUS", headerFont, Element.ALIGN_CENTER);
        AddHeaderCell(table, "LAST ACTIVITY", headerFont, Element.ALIGN_RIGHT);

        int rowIndex = 0;
        AppendBalanceGroup(table, owingTabs, "Outstanding", Owing, OwingSoft, ref rowIndex);
        AppendBalanceGroup(table, creditTabs, "In Credit", Credit, CreditSoft, ref rowIndex);
        AppendBalanceGroup(table, settledTabs, "Settled", Neutral, NeutralSoft, ref rowIndex);

        // Totals row: owing total in red, credit total in green.
        var totalLabelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10f, BrandPrimary);
        var totalOwingFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10f, Owing);
        var totalCreditFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9.5f, Credit);

        var totalLabelCell = new PdfPCell(new Phrase("TOTAL", totalLabelFont))
        {
            BackgroundColor = BrandSoft,
            HorizontalAlignment = Element.ALIGN_LEFT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 7,
            PaddingBottom = 7,
            PaddingLeft = 8,
            BorderColor = BorderColor,
        };
        table.AddCell(totalLabelCell);

        var totalOwingCell = new PdfPCell(new Phrase(FormatCurrency(totalOwing), totalOwingFont))
        {
            BackgroundColor = BrandSoft,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 7,
            PaddingBottom = 7,
            PaddingRight = 8,
            BorderColor = BorderColor,
        };
        table.AddCell(totalOwingCell);

        var creditNote = creditTabs.Count > 0
            ? $"+ {FormatCurrency(totalCredit)} credit"
            : "—";
        var creditNoteCell = new PdfPCell(new Phrase(creditNote, totalCreditFont))
        {
            BackgroundColor = BrandSoft,
            HorizontalAlignment = Element.ALIGN_CENTER,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 7,
            PaddingBottom = 7,
            BorderColor = BorderColor,
        };
        table.AddCell(creditNoteCell);

        var blankFont = FontFactory.GetFont(FontFactory.HELVETICA, 9f, BrandSoft);
        table.AddCell(new PdfPCell(new Phrase(" ", blankFont))
        {
            BackgroundColor = BrandSoft,
            BorderColor = BorderColor,
        });

        doc.Add(table);
    }

    private static void AppendBalanceGroup(
        PdfPTable table,
        List<TabRow> rows,
        string status,
        BaseColor statusFg,
        BaseColor statusBg,
        ref int rowIndex)
    {
        var nameFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10f, TextPrimary);
        var balanceFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10f, statusFg);
        var settledFont = FontFactory.GetFont(FontFactory.HELVETICA, 10f, Muted);
        var statusFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7.8f, statusFg);
        var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 9f, Muted);

        var isSettled = status.Equals("Settled", StringComparison.OrdinalIgnoreCase);

        foreach (var tab in rows)
        {
            var rowBg = rowIndex % 2 == 0 ? White : ZebraRow;

            // Member name
            AddBodyCell(table, FormatTabName(tab.Name), nameFont, rowBg, Element.ALIGN_LEFT, paddingLeft: 8);

            // Balance (right-aligned, currency)
            AddBodyCell(table, FormatCurrency(tab.Balance), isSettled ? settledFont : balanceFont, rowBg, Element.ALIGN_RIGHT, paddingRight: 8);

            // Status pill (colored background with rounded look via padding)
            var statusCell = new PdfPCell
            {
                BackgroundColor = rowBg,
                BorderColor = BorderColor,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                PaddingTop = 4,
                PaddingBottom = 4,
            };
            var pill = new PdfPTable(1) { WidthPercentage = 100 };
            pill.AddCell(new PdfPCell(new Phrase(status.ToUpperInvariant(), statusFont))
            {
                BackgroundColor = statusBg,
                BorderColor = statusBg,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                PaddingTop = 4,
                PaddingBottom = 4,
                PaddingLeft = 6,
                PaddingRight = 6,
                NoWrap = true,
            });
            statusCell.AddElement(pill);
            table.AddCell(statusCell);

            // Last activity (right-aligned date)
            AddBodyCell(table, FormatActivity(tab.LastActivityAt), dateFont, rowBg, Element.ALIGN_RIGHT, paddingRight: 8);

            rowIndex++;
        }
    }

    private static void AddHeaderCell(PdfPTable table, string text, PdfFont font, int alignment)
    {
        table.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = BrandPrimary,
            BorderColor = BrandPrimary,
            HorizontalAlignment = alignment,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 7,
            PaddingBottom = 7,
            PaddingLeft = 8,
            PaddingRight = 8,
        });
    }

    private static void AddBodyCell(
        PdfPTable table,
        string text,
        PdfFont font,
        BaseColor bg,
        int alignment,
        float paddingLeft = 6,
        float paddingRight = 6)
    {
        table.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = bg,
            BorderColor = BorderColor,
            HorizontalAlignment = alignment,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 5,
            PaddingBottom = 5,
            PaddingLeft = paddingLeft,
            PaddingRight = paddingRight,
        });
    }

    // -------- Footer --------------------------------------------------------

    private static void AddFooter(
        Document doc,
        int totalMembers,
        int currentMonthActivity,
        int creditCount,
        int owingCount)
    {
        var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, Muted);
        doc.Add(new Paragraph(
            $"Total members: {totalMembers}    |    Active this month: {currentMonthActivity}    |    In credit: {creditCount}    |    Owing: {owingCount}",
            summaryFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingBefore = 10,
            SpacingAfter = 2,
        });

        var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, Muted);
        doc.Add(new Paragraph(
            $"Generated by {ClubName} POS · {DateTime.Now.ToString("dddd d MMMM yyyy 'at' h:mm tt", CurrencyCulture)}",
            footerFont)
        {
            Alignment = Element.ALIGN_CENTER,
        });
    }

    // -------- Data loading --------------------------------------------------

    private static List<TabRow> LoadTabs(SqliteConnectionFactory factory, DateTime monthStart, DateTime monthEnd)
    {
        using var conn = factory.OpenConnection();

        var rows = conn.Query<RawTabRow>(
            """
            SELECT
              Id                                          AS Id,
              COALESCE(DisplayName, Name)                 AS Name,
              COALESCE(Balance, 0)                        AS Balance,
              LastActivityAt                              AS LastActivityAt
            FROM Tabs
            WHERE COALESCE(IsDeleted, 0) = 0
              AND COALESCE(IsClosed, 0) = 0
              AND COALESCE(IsArchived, 0) = 0
            ORDER BY COALESCE(DisplayName, Name) COLLATE NOCASE ASC
            """).ToList();

        var entryMonthIds = new HashSet<long>(
            conn.Query<long>(
                """
                SELECT DISTINCT TabId
                FROM TabEntries
                WHERE COALESCE(OccurredAt, CreatedAt) >= @Start
                  AND COALESCE(OccurredAt, CreatedAt) <= @End
                """,
                new
                {
                    Start = monthStart.ToString("o"),
                    End = monthEnd.AddDays(1).AddTicks(-1).ToString("o"),
                }));

        var movementMonthIds = new HashSet<long>(
            conn.Query<long>(
                """
                SELECT DISTINCT TabId
                FROM MoneyMovements
                WHERE TabId IS NOT NULL
                  AND COALESCE(OccurredAt, CreatedAt) >= @Start
                  AND COALESCE(OccurredAt, CreatedAt) <= @End
                """,
                new
                {
                    Start = monthStart.ToString("o"),
                    End = monthEnd.AddDays(1).AddTicks(-1).ToString("o"),
                }));

        return rows
            .Select(r => new TabRow
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                Balance = (decimal)r.Balance,
                LastActivityAt = ParseDate(r.LastActivityAt),
                HadCurrentMonthActivity = entryMonthIds.Contains(r.Id) || movementMonthIds.Contains(r.Id),
            })
            .ToList();
    }

    // -------- Helpers -------------------------------------------------------

    private static string FormatCurrency(decimal amount) => amount.ToString("C", CurrencyCulture);

    private static string FormatTabName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Unknown";
        }

        return name.Replace('_', ' ').Trim();
    }

    private static string FormatActivity(DateTime? when) =>
        when is null ? "—" : when.Value.ToString("d MMM yyyy", CurrencyCulture);

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTime.TryParse(raw, CurrencyCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt.ToLocalTime();
        }

        return null;
    }

    private sealed class RawTabRow
    {
        public long Id { get; set; }

        public string? Name { get; set; }

        public double Balance { get; set; }

        public string? LastActivityAt { get; set; }
    }

    private sealed class TabRow
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public DateTime? LastActivityAt { get; set; }

        public bool HadCurrentMonthActivity { get; set; }
    }
}

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
/// Compact stock-snapshot PDF (A4) for weekly email. Brand header, one-line summary stats,
/// a dense "Needs restocking" callout when anything is out or low, then in-stock items
/// grouped by category (out/low are not repeated below the callout). Uses catalog bucket/sub-category
/// labels and per-item low-stock thresholds (default 5).
/// </summary>
internal static class StockSnapshotPdfBuilder
{
    private const string ClubName = "Nickeltown Flounderers";

    private const string ReportTitle = "Stock Snapshot";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    // ----- Brand palette ----------------------------------------------------
    private static readonly BaseColor BrandPrimary = new(30, 58, 138);     // #1E3A8A
    private static readonly BaseColor BrandAccent = new(59, 130, 246);     // #3B82F6
    private static readonly BaseColor BrandSoft = new(238, 242, 255);      // #EEF2FF
    private static readonly BaseColor DangerFg = new(185, 28, 28);         // #B91C1C
    private static readonly BaseColor DangerBg = new(254, 226, 226);       // #FEE2E2
    private static readonly BaseColor WarnFg = new(180, 83, 9);            // #B45309
    private static readonly BaseColor WarnBg = new(254, 243, 199);         // #FEF3C7
    private static readonly BaseColor OkFg = new(21, 128, 61);             // #15803D
    private static readonly BaseColor OkBg = new(220, 252, 231);           // #DCFCE7
    private static readonly BaseColor Neutral = new(75, 85, 99);           // #4B5563
    private static readonly BaseColor NeutralSoft = new(243, 244, 246);    // #F3F4F6
    private static readonly BaseColor Muted = new(107, 114, 128);          // #6B7280
    private static readonly BaseColor TextPrimary = new(17, 24, 39);       // #111827
    private static readonly BaseColor BorderColor = new(229, 231, 235);    // #E5E7EB
    private static readonly BaseColor ZebraRow = new(249, 250, 251);       // #F9FAFB
    private static readonly BaseColor White = new(255, 255, 255);

    public static byte[] Build(SqliteConnectionFactory factory)
    {
        var rows = LoadStock(factory);

        using var ms = new MemoryStream();
        using (var doc = new Document(PageSize.A4, 24, 24, 26, 26))
        {
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            AddHeader(doc);

            var outOfStock = rows
                .Where(r => r.TrackStock && r.StockQty <= 0)
                .OrderBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var lowStock = rows
                .Where(r => r.TrackStock && r.StockQty > 0 && r.StockQty <= r.LowStockThreshold)
                .OrderBy(r => r.StockQty)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var inStockCount = rows.Count - outOfStock.Count - lowStock.Count;
            var categories = rows.Select(r => r.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var hasRestockItems = outOfStock.Count > 0 || lowStock.Count > 0;

            AddCompactSummaryBar(doc, rows.Count, inStockCount, lowStock.Count, outOfStock.Count, categories);

            if (rows.Count == 0)
            {
                doc.Add(new Paragraph(
                    "No items found in the catalog.",
                    FontFactory.GetFont(FontFactory.HELVETICA, 11, Muted))
                {
                    SpacingBefore = 12,
                });
                return ms.ToArray();
            }

            if (hasRestockItems)
            {
                AddSectionHeader(doc, "NEEDS RESTOCKING", DangerFg);
                AddNeedsRestockingCallout(doc, outOfStock, lowStock);
            }

            var catalogRows = hasRestockItems
                ? rows.Where(r => !NeedsRestocking(r)).ToList()
                : rows;
            if (catalogRows.Count > 0)
            {
                AddSectionHeader(
                    doc,
                    hasRestockItems ? "IN STOCK BY CATEGORY" : "STOCK BY CATEGORY",
                    BrandPrimary);
                AddCatalogTable(doc, catalogRows);
            }

            AddFooter(doc);
        }

        return ms.ToArray();
    }

    // -------- Header --------------------------------------------------------

    private static void AddHeader(Document doc)
    {
        var header = new PdfPTable(2) { WidthPercentage = 100 };
        header.SetWidths(new float[] { 1.1f, 3f });
        header.SpacingAfter = 4;

        header.AddCell(ReportPdfBranding.CreateLogoCell(maxWidth: 56f, maxHeight: 56f));

        var titleCell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            Padding = 3,
        };

        var clubFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BrandAccent);
        titleCell.AddElement(new Paragraph(ClubName.ToUpperInvariant(), clubFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 1,
        });

        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 17, BrandPrimary);
        titleCell.AddElement(new Paragraph(ReportTitle, titleFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 1,
        });

        var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, Muted);
        titleCell.AddElement(new Paragraph(
            DateTime.Now.ToString("dddd d MMMM yyyy 'at' h:mm tt", Culture),
            subtitleFont)
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
        bar.SpacingBefore = 4;
        bar.SpacingAfter = 6;
        bar.AddCell(new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            BackgroundColor = BrandPrimary,
            FixedHeight = 3f,
        });
        doc.Add(bar);
    }

    // -------- Summary bar ---------------------------------------------------

    private static void AddCompactSummaryBar(
        Document doc,
        int totalItems,
        int inStock,
        int lowStock,
        int outOfStock,
        int categories)
    {
        var table = new PdfPTable(1) { WidthPercentage = 100, SpacingAfter = 8 };
        var text =
            $"Total {totalItems}  ·  In stock {inStock}  ·  Low {lowStock}  ·  Out {outOfStock}  ·  {categories} categor{(categories == 1 ? "y" : "ies")}";
        var font = FontFactory.GetFont(FontFactory.HELVETICA, 9f, TextPrimary);
        table.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = BrandSoft,
            Border = Rectangle.NO_BORDER,
            BorderWidthLeft = 3f,
            BorderColorLeft = BrandPrimary,
            PaddingTop = 5,
            PaddingBottom = 5,
            PaddingLeft = 10,
            PaddingRight = 10,
            HorizontalAlignment = Element.ALIGN_CENTER,
        });
        doc.Add(table);
    }

    private static bool NeedsRestocking(StockRow row) =>
        row.TrackStock && row.StockQty <= row.LowStockThreshold;

    // -------- Section header -----------------------------------------------

    private static void AddSectionHeader(Document doc, string text, BaseColor color)
    {
        var block = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 3, SpacingAfter = 4 };
        var font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9.5f, White);
        block.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = color,
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_LEFT,
            PaddingTop = 4,
            PaddingBottom = 4,
            PaddingLeft = 8,
        });
        doc.Add(block);
    }

    // -------- "Needs restocking" callout -----------------------------------

    /// <summary>
    /// Compact 3-column callout: Item / Category / Qty. Out-of-stock items first (red qty),
    /// then low-stock (amber qty). Intentionally narrower and lighter than the full catalog
    /// table so it reads as a quick action list, not a duplicate inventory listing.
    /// </summary>
    private static void AddNeedsRestockingCallout(Document doc, List<StockRow> outOfStock, List<StockRow> lowStock)
    {
        var table = new PdfPTable(3) { WidthPercentage = 100, SpacingAfter = 8 };
        table.SetWidths(new float[] { 4.6f, 2.6f, 0.8f });
        table.HeaderRows = 1;

        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, White);
        AddCalloutHeader(table, "ITEM", headerFont, Element.ALIGN_LEFT);
        AddCalloutHeader(table, "CATEGORY", headerFont, Element.ALIGN_LEFT);
        AddCalloutHeader(table, "QTY", headerFont, Element.ALIGN_RIGHT);

        int rowIndex = 0;
        foreach (var row in outOfStock)
        {
            AppendCalloutRow(table, row, DangerFg, ref rowIndex);
        }

        foreach (var row in lowStock)
        {
            AppendCalloutRow(table, row, WarnFg, ref rowIndex);
        }

        doc.Add(table);
    }

    private static void AddCalloutHeader(PdfPTable table, string text, PdfFont font, int alignment)
    {
        table.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = DangerFg,
            BorderColor = DangerFg,
            HorizontalAlignment = alignment,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 3,
            PaddingBottom = 3,
            PaddingLeft = 6,
            PaddingRight = 6,
        });
    }

    private static void AppendCalloutRow(PdfPTable table, StockRow row, BaseColor qtyColor, ref int rowIndex)
    {
        var rowBg = rowIndex % 2 == 0 ? White : ZebraRow;
        var nameFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, TextPrimary);
        var catFont = FontFactory.GetFont(FontFactory.HELVETICA, 8f, Muted);
        var qtyFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, qtyColor);

        AddBodyCell(table, row.Name, nameFont, rowBg, Element.ALIGN_LEFT, paddingLeft: 8);
        AddBodyCell(table, row.Category, catFont, rowBg, Element.ALIGN_LEFT);
        AddBodyCell(table, row.StockQty.ToString(CultureInfo.InvariantCulture), qtyFont, rowBg, Element.ALIGN_RIGHT, paddingRight: 8);

        rowIndex++;
    }

    // -------- Catalog table -------------------------------------------------

    private static void AddCatalogTable(Document doc, List<StockRow> rows)
    {
        var table = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 4 };
        table.SetWidths(new float[] { 6f, 1f });
        table.HeaderRows = 1;

        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, White);
        AddHeaderCell(table, "ITEM", headerFont, Element.ALIGN_LEFT);
        AddHeaderCell(table, "QTY", headerFont, Element.ALIGN_RIGHT);

        int rowIndex = 0;
        string currentCategory = string.Empty;
        var grouped = rows
            .OrderBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Pre-count items per category for the group row.
        var perCategoryCount = grouped
            .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in grouped)
        {
            if (!row.Category.Equals(currentCategory, StringComparison.OrdinalIgnoreCase))
            {
                currentCategory = row.Category;
                AddCategoryGroupRow(table, currentCategory, perCategoryCount[currentCategory]);
                rowIndex = 0;
            }

            AppendCatalogRow(table, row, ref rowIndex);
        }

        doc.Add(table);
    }

    private static void AddCategoryGroupRow(PdfPTable table, string category, int itemCount)
    {
        var font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, White);
        var countFont = FontFactory.GetFont(FontFactory.HELVETICA, 7.5f, new BaseColor(199, 210, 254));

        var inner = new PdfPTable(2) { WidthPercentage = 100 };
        inner.SetWidths(new float[] { 5, 1 });

        inner.AddCell(new PdfPCell(new Phrase(category.ToUpperInvariant(), font))
        {
            Border = Rectangle.NO_BORDER,
            BackgroundColor = BrandAccent,
            HorizontalAlignment = Element.ALIGN_LEFT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 3,
            PaddingBottom = 3,
            PaddingLeft = 6,
        });
        inner.AddCell(new PdfPCell(new Phrase($"{itemCount} item{(itemCount == 1 ? "" : "s")}", countFont))
        {
            Border = Rectangle.NO_BORDER,
            BackgroundColor = BrandAccent,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 3,
            PaddingBottom = 3,
            PaddingRight = 6,
        });

        var outer = new PdfPCell(inner)
        {
            Colspan = 2,
            Padding = 0,
            BackgroundColor = BrandAccent,
            BorderColor = BrandAccent,
        };
        table.AddCell(outer);
    }

    private static void AppendCatalogRow(PdfPTable table, StockRow row, ref int rowIndex)
    {
        var rowBg = rowIndex % 2 == 0 ? White : ZebraRow;
        var nameFont = FontFactory.GetFont(
            FontFactory.HELVETICA,
            8.5f,
            row.TrackStock ? TextPrimary : Muted);
        var qtyText = row.TrackStock
            ? row.StockQty.ToString(CultureInfo.InvariantCulture)
            : "—";
        var qtyFont = FontFactory.GetFont(
            FontFactory.HELVETICA,
            8.5f,
            row.TrackStock ? TextPrimary : Muted);

        AddBodyCell(table, row.Name, nameFont, rowBg, Element.ALIGN_LEFT, paddingLeft: 8);
        AddBodyCell(table, qtyText, qtyFont, rowBg, Element.ALIGN_RIGHT, paddingRight: 6);

        rowIndex++;
    }

    // -------- Generic table helpers ----------------------------------------

    private static void AddHeaderCell(PdfPTable table, string text, PdfFont font, int alignment)
    {
        table.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = BrandPrimary,
            BorderColor = BrandPrimary,
            HorizontalAlignment = alignment,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 4,
            PaddingBottom = 4,
            PaddingLeft = 6,
            PaddingRight = 6,
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
            PaddingTop = 3,
            PaddingBottom = 3,
            PaddingLeft = paddingLeft,
            PaddingRight = paddingRight,
        });
    }

    // -------- Footer --------------------------------------------------------

    private static void AddFooter(Document doc)
    {
        var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 7.5f, Muted);
        doc.Add(new Paragraph(
            $"Generated by {ClubName} POS · {DateTime.Now.ToString("dddd d MMMM yyyy 'at' h:mm tt", Culture)}",
            footerFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingBefore = 6,
        });
    }

    // -------- Data loading --------------------------------------------------

    private static List<StockRow> LoadStock(SqliteConnectionFactory factory)
    {
        using var conn = factory.OpenConnection();

        var rows = conn.Query<RawStockRow>(
            $"""
            SELECT
              i.Id                                  AS Id,
              i.Name                                AS Name,
              i.Sku                                 AS Sku,
              {StockSnapshotQuery.CategorySelectExpr} AS Category,
              i.ItemType                            AS ItemType,
              i.StockQty                            AS StockQty,
              COALESCE(i.TrackStock, 1)             AS TrackStock,
              i.LowStockThreshold                     AS LowStockThreshold
            FROM Items i
            {StockSnapshotQuery.ReportWhereClause}
            {StockSnapshotQuery.OrderByClause}
            """).ToList();

        return rows
            .Select(r => new StockRow
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                Sku = r.Sku,
                Category = string.IsNullOrWhiteSpace(r.Category) ? "Bar / Drinks" : r.Category,
                ItemType = r.ItemType ?? "Item",
                StockQty = (int)r.StockQty,
                TrackStock = r.TrackStock != 0,
                LowStockThreshold = StockSnapshotQuery.EffectiveLowStockThreshold(
                    r.LowStockThreshold is > 0 ? (int)r.LowStockThreshold : null),
            })
            .ToList();
    }

    private sealed class RawStockRow
    {
        public long Id { get; set; }

        public string? Name { get; set; }

        public string? Sku { get; set; }

        public string? Category { get; set; }

        public string? ItemType { get; set; }

        public long StockQty { get; set; }

        public long TrackStock { get; set; }

        public long? LowStockThreshold { get; set; }
    }

    private sealed class StockRow
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Sku { get; set; }

        public string Category { get; set; } = "Bar / Drinks";

        public string ItemType { get; set; } = "Item";

        public int StockQty { get; set; }

        public bool TrackStock { get; set; }

        public int LowStockThreshold { get; set; } = 5;
    }
}

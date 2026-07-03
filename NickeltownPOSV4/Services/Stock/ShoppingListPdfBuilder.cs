using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;
using PdfFont = iTextSharp.text.Font;

namespace NickeltownPOSV4.Services.Stock;

internal enum ShoppingListPdfScope
{
    Regular,
    Merch,
}

/// <summary>
/// Branded shopping-list PDF (A4). Summary cards, then a compact table of items to buy
/// with have/need/suggested columns and status pills.
/// </summary>
internal static class ShoppingListPdfBuilder
{
    private const string ClubName = "Nickeltown Flounderers";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    private static readonly BaseColor BrandPrimary = new(30, 58, 138);
    private static readonly BaseColor BrandAccent = new(59, 130, 246);
    private static readonly BaseColor BrandSoft = new(238, 242, 255);
    private static readonly BaseColor DangerFg = new(185, 28, 28);
    private static readonly BaseColor DangerBg = new(254, 226, 226);
    private static readonly BaseColor WarnFg = new(180, 83, 9);
    private static readonly BaseColor WarnBg = new(254, 243, 199);
    private static readonly BaseColor OkFg = new(21, 128, 61);
    private static readonly BaseColor OkBg = new(220, 252, 231);
    private static readonly BaseColor Neutral = new(75, 85, 99);
    private static readonly BaseColor NeutralSoft = new(243, 244, 246);
    private static readonly BaseColor Muted = new(107, 114, 128);
    private static readonly BaseColor TextPrimary = new(17, 24, 39);
    private static readonly BaseColor BorderColor = new(229, 231, 235);
    private static readonly BaseColor ZebraRow = new(249, 250, 251);
    private static readonly BaseColor White = new(255, 255, 255);

    private static readonly BaseColor MerchAccent = new(124, 58, 237);     // #7C3AED

    public static byte[] Build(IReadOnlyList<StockShoppingListRowViewModel> rows, ShoppingListPdfScope scope)
    {
        rows = scope == ShoppingListPdfScope.Merch
            ? rows.Where(r => r.IsMerch).ToList()
            : rows.Where(r => !r.IsMerch).ToList();

        var reportTitle = scope == ShoppingListPdfScope.Merch ? "Merchandise" : "Bar & Supplies";
        var accentColor = scope == ShoppingListPdfScope.Merch ? MerchAccent : BrandPrimary;

        using var ms = new MemoryStream();
        using (var doc = new Document(PageSize.A4, 30, 30, 32, 32))
        {
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            AddHeader(doc, reportTitle, accentColor);

            var outOfStock = rows.Count(r => r.Status == StockVolunteerStatus.OutOfStock);
            var buyNow = rows.Count(r => r.Status == StockVolunteerStatus.BuyNow);
            var setupWarnings = rows.Count(r => r.HasSetupWarning);
            var totalNeed = rows.Sum(r => r.NeedQty);

            AddSummaryCards(doc, rows.Count, outOfStock, buyNow, totalNeed, scope);

            if (rows.Count == 0)
            {
                doc.Add(new Paragraph(
                    scope == ShoppingListPdfScope.Merch
                        ? "No merchandise needs buying right now."
                        : "No bar & supplies need buying right now.",
                    FontFactory.GetFont(FontFactory.HELVETICA, 12, Muted))
                {
                    SpacingBefore = 20,
                });
            }
            else
            {
                if (setupWarnings > 0)
                {
                    var warnFont = FontFactory.GetFont(FontFactory.HELVETICA, 9.5f, WarnFg);
                    doc.Add(new Paragraph(
                        $"{setupWarnings} item{(setupWarnings == 1 ? "" : "s")} need pack size set in product setup before ordering.",
                        warnFont)
                    {
                        SpacingBefore = 4,
                        SpacingAfter = 8,
                    });
                }

                if (scope == ShoppingListPdfScope.Merch)
                {
                    var noteFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9f, Muted);
                    doc.Add(new Paragraph("Ordered less often — hats, shirts, and club merch.", noteFont)
                    {
                        SpacingBefore = 0,
                        SpacingAfter = 8,
                    });
                }

                AddSectionHeader(doc, "ITEMS TO BUY", accentColor);
                AddShoppingTable(doc, rows, accentColor);
            }

            AddFooter(doc, rows.Count, totalNeed, reportTitle);
        }

        return ms.ToArray();
    }

    private static void AddHeader(Document doc, string reportTitle, BaseColor accentColor)
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

        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 22, accentColor);
        titleCell.AddElement(new Paragraph(reportTitle, titleFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 1,
        });

        var listFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, Muted);
        titleCell.AddElement(new Paragraph("Shopping List", listFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 2,
        });

        var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, Muted);
        titleCell.AddElement(new Paragraph(
            DateTime.Now.ToString("dddd d MMMM yyyy 'at' h:mm tt", Culture),
            subtitleFont)
        {
            Alignment = Element.ALIGN_RIGHT,
        });

        header.AddCell(titleCell);
        doc.Add(header);
        AddBrandBar(doc, accentColor);
    }

    private static void AddBrandBar(Document doc, BaseColor accentColor)
    {
        var bar = new PdfPTable(1) { WidthPercentage = 100 };
        bar.SpacingBefore = 6;
        bar.SpacingAfter = 10;
        bar.AddCell(new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            BackgroundColor = accentColor,
            FixedHeight = 3f,
        });
        doc.Add(bar);
    }

    private static void AddSummaryCards(
        Document doc,
        int totalItems,
        int outOfStock,
        int buyNow,
        int totalNeed,
        ShoppingListPdfScope scope)
    {
        var table = new PdfPTable(4) { WidthPercentage = 100 };
        table.SetWidths(new float[] { 1, 1, 1, 1 });
        table.SpacingAfter = 14;

        var accent = scope == ShoppingListPdfScope.Merch ? MerchAccent : BrandPrimary;
        var softBg = scope == ShoppingListPdfScope.Merch ? new BaseColor(245, 243, 255) : BrandSoft;

        AddSummaryCard(table, "ITEMS",
            totalItems.ToString(CultureInfo.InvariantCulture),
            softBg, accent, accent);
        AddSummaryCard(table, "OUT OF STOCK",
            outOfStock.ToString(CultureInfo.InvariantCulture),
            DangerBg, DangerFg, DangerFg);
        AddSummaryCard(table, "BUY NOW",
            buyNow.ToString(CultureInfo.InvariantCulture),
            DangerBg, DangerFg, DangerFg);
        AddSummaryCard(table, "UNITS NEEDED",
            totalNeed.ToString(CultureInfo.InvariantCulture),
            OkBg, OkFg, OkFg);

        doc.Add(table);
    }

    private static void AddSummaryCard(
        PdfPTable parent,
        string label,
        string value,
        BaseColor bg,
        BaseColor accent,
        BaseColor valueColor)
    {
        var inner = new PdfPTable(1) { WidthPercentage = 100 };
        var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7.5f, accent);
        var valueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18f, valueColor);

        inner.AddCell(new PdfPCell(new Phrase(label, labelFont))
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_CENTER,
            BackgroundColor = bg,
            PaddingTop = 9,
            PaddingBottom = 2,
        });
        inner.AddCell(new PdfPCell(new Phrase(value, valueFont))
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_CENTER,
            BackgroundColor = bg,
            PaddingTop = 2,
            PaddingBottom = 9,
        });

        parent.AddCell(new PdfPCell(inner)
        {
            Padding = 0,
            BackgroundColor = bg,
            BorderColor = accent,
            BorderWidthLeft = 3f,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
        });
    }

    private static void AddSectionHeader(Document doc, string text, BaseColor color)
    {
        var block = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 4, SpacingAfter = 6 };
        var font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10.5f, White);
        block.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = color,
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_LEFT,
            PaddingTop = 6,
            PaddingBottom = 6,
            PaddingLeft = 10,
        });
        doc.Add(block);
    }

    private static void AddShoppingTable(Document doc, IReadOnlyList<StockShoppingListRowViewModel> rows, BaseColor headerColor)
    {
        var table = new PdfPTable(5) { WidthPercentage = 100, SpacingAfter = 6 };
        table.SetWidths(new float[] { 4.2f, 0.8f, 0.8f, 2.4f, 1.4f });
        table.HeaderRows = 1;

        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9.5f, White);
        AddHeaderCell(table, "ITEM", headerFont, Element.ALIGN_LEFT, headerColor);
        AddHeaderCell(table, "HAVE", headerFont, Element.ALIGN_CENTER, headerColor);
        AddHeaderCell(table, "NEED", headerFont, Element.ALIGN_CENTER, headerColor);
        AddHeaderCell(table, "SUGGESTED", headerFont, Element.ALIGN_LEFT, headerColor);
        AddHeaderCell(table, "STATUS", headerFont, Element.ALIGN_CENTER, headerColor);

        int rowIndex = 0;
        foreach (var row in rows)
        {
            AppendRow(table, row, ref rowIndex);
        }

        doc.Add(table);
    }

    private static void AppendRow(PdfPTable table, StockShoppingListRowViewModel row, ref int rowIndex)
    {
        var rowBg = rowIndex % 2 == 0 ? White : ZebraRow;
        var nameFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9.5f, TextPrimary);
        var catFont = FontFactory.GetFont(FontFactory.HELVETICA, 8f, Muted);
        var qtyFont = FontFactory.GetFont(FontFactory.HELVETICA, 9.5f, TextPrimary);
        var needFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9.5f, TextPrimary);
        var suggestedFont = FontFactory.GetFont(
            FontFactory.HELVETICA,
            9f,
            row.HasSetupWarning ? WarnFg : Muted);

        var nameCell = new PdfPCell
        {
            BackgroundColor = rowBg,
            BorderColor = BorderColor,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 5,
            PaddingBottom = 5,
            PaddingLeft = 8,
            PaddingRight = 6,
        };
        nameCell.AddElement(new Paragraph(row.Name, nameFont) { SpacingAfter = 1 });
        if (!string.IsNullOrWhiteSpace(row.CategoryLine))
        {
            nameCell.AddElement(new Paragraph(row.CategoryLine, catFont));
        }

        table.AddCell(nameCell);
        AddBodyCell(table, row.HaveQty.ToString(CultureInfo.InvariantCulture), qtyFont, rowBg, Element.ALIGN_CENTER);
        AddBodyCell(table, row.NeedQty.ToString(CultureInfo.InvariantCulture), needFont, rowBg, Element.ALIGN_CENTER);

        var suggested = row.HasSetupWarning
            ? "Pack size not set"
            : (string.IsNullOrEmpty(row.SuggestedLine) ? "—" : row.SuggestedLine);
        AddBodyCell(table, suggested, suggestedFont, rowBg, Element.ALIGN_LEFT, paddingLeft: 8);

        var (statusText, statusFg, statusBg) = StatusStyle(row);
        var pillFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7.5f, statusFg);
        AddPillCell(table, statusText, pillFont, statusBg, rowBg);

        rowIndex++;
    }

    private static (string Text, BaseColor Fg, BaseColor Bg) StatusStyle(StockShoppingListRowViewModel row) =>
        row.Status switch
        {
            StockVolunteerStatus.OutOfStock => ("OUT", DangerFg, DangerBg),
            StockVolunteerStatus.BuyNow => ("BUY NOW", DangerFg, DangerBg),
            StockVolunteerStatus.BuySoon => ("BUY SOON", WarnFg, WarnBg),
            _ => ("OK", OkFg, OkBg),
        };

    private static void AddHeaderCell(PdfPTable table, string text, PdfFont font, int alignment, BaseColor headerColor)
    {
        table.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = headerColor,
            BorderColor = headerColor,
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

    private static void AddPillCell(PdfPTable table, string text, PdfFont pillFont, BaseColor pillBg, BaseColor rowBg)
    {
        var pill = new PdfPTable(1) { WidthPercentage = 85 };
        pill.AddCell(new PdfPCell(new Phrase(text, pillFont))
        {
            BackgroundColor = pillBg,
            BorderColor = pillBg,
            HorizontalAlignment = Element.ALIGN_CENTER,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 3,
            PaddingBottom = 3,
        });

        var cell = new PdfPCell
        {
            BackgroundColor = rowBg,
            BorderColor = BorderColor,
            HorizontalAlignment = Element.ALIGN_CENTER,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 4,
            PaddingBottom = 4,
        };
        cell.AddElement(pill);
        table.AddCell(cell);
    }

    private static void AddFooter(Document doc, int totalItems, int totalNeed, string reportTitle)
    {
        var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, Muted);
        doc.Add(new Paragraph(
            $"{reportTitle}: {totalItems} item{(totalItems == 1 ? "" : "s")}    |    Units needed: {totalNeed}",
            summaryFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingBefore = 10,
            SpacingAfter = 2,
        });

        var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, Muted);
        doc.Add(new Paragraph(
            $"Generated by {ClubName} POS · {DateTime.Now.ToString("dddd d MMMM yyyy 'at' h:mm tt", Culture)}",
            footerFont)
        {
            Alignment = Element.ALIGN_CENTER,
        });
    }
}

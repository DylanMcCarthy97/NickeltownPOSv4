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

/// <summary>
/// Compact purchasing-checklist PDF (A4). Bar and Merchandise lists share the same layout
/// with different titles — never mixed into one document.
/// </summary>
internal static class ShoppingListPdfBuilder
{
    private const string ClubName = "Nickeltown Flounderers";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    private static readonly BaseColor BrandPrimary = new(30, 58, 138);   // #1E3A8A
    private static readonly BaseColor BrandAccent = new(59, 130, 246);  // #3B82F6
    private static readonly BaseColor DangerFg = new(185, 28, 28);
    private static readonly BaseColor DangerBg = new(254, 226, 226);
    private static readonly BaseColor WarnFg = new(180, 83, 9);
    private static readonly BaseColor WarnBg = new(254, 243, 199);
    private static readonly BaseColor Muted = new(107, 114, 128);
    private static readonly BaseColor TextPrimary = new(17, 24, 39);
    private static readonly BaseColor BorderColor = new(226, 232, 240);
    private static readonly BaseColor ZebraRow = new(248, 250, 252);
    private static readonly BaseColor White = new(255, 255, 255);
    private static readonly BaseColor HeaderBg = new(30, 58, 138);

    public static byte[] Build(ShoppingListSnapshot snapshot) =>
        Build(snapshot.Kind, snapshot.PurchaseRows, snapshot.MissingPackSizeNames, snapshot.InvalidReorderNames);

    public static byte[] Build(
        ShoppingListKind kind,
        IReadOnlyList<StockShoppingListRowViewModel> purchaseRows,
        IReadOnlyList<string>? missingPackSizeNames = null,
        IReadOnlyList<string>? invalidReorderNames = null)
    {
        missingPackSizeNames ??= Array.Empty<string>();
        invalidReorderNames ??= Array.Empty<string>();

        var title = ShoppingListCatalog.DisplayTitle(kind);
        var buyNow = purchaseRows
            .Where(r => r.Status is StockVolunteerStatus.OutOfStock or StockVolunteerStatus.BuyNow)
            .ToList();
        var buySoon = purchaseRows
            .Where(r => r.Status == StockVolunteerStatus.BuySoon)
            .ToList();
        var totalUnits = purchaseRows.Sum(r => r.UnitsToBuy);

        using var ms = new MemoryStream();
        // Tight margins for a purchasing checklist (still readable when printed).
        using (var doc = new Document(PageSize.A4, 36f, 36f, 28f, 28f))
        {
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            AddCompactHeader(doc, title);
            AddSummaryLine(doc, purchaseRows.Count, totalUnits);

            if (purchaseRows.Count == 0)
            {
                doc.Add(new Paragraph(
                    "Nothing needs buying right now.",
                    FontFactory.GetFont(FontFactory.HELVETICA, 10f, Muted))
                {
                    SpacingBefore = 10f,
                });
            }
            else
            {
                if (buyNow.Count > 0)
                {
                    AddSectionLabel(doc, "BUY NOW", DangerFg, DangerBg);
                    AddCompactTable(doc, buyNow);
                }

                if (buySoon.Count > 0)
                {
                    AddSectionLabel(doc, "BUY SOON", WarnFg, WarnBg);
                    AddCompactTable(doc, buySoon);
                }
            }

            AddFooterNotes(doc, missingPackSizeNames, invalidReorderNames);
        }

        return ms.ToArray();
    }

    private static void AddCompactHeader(Document doc, string title)
    {
        var header = new PdfPTable(2) { WidthPercentage = 100f };
        header.SetWidths(new float[] { 0.9f, 3.1f });
        header.SpacingAfter = 2f;

        header.AddCell(ReportPdfBranding.CreateLogoCell(maxWidth: 52f, maxHeight: 40f));

        var titleCell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            Padding = 2f,
        };

        titleCell.AddElement(new Paragraph(
            ClubName,
            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10f, BrandPrimary))
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 1f,
        });
        titleCell.AddElement(new Paragraph(
            title,
            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14f, TextPrimary))
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 1f,
        });
        titleCell.AddElement(new Paragraph(
            DateTime.Now.ToString("dddd d MMMM yyyy", Culture),
            FontFactory.GetFont(FontFactory.HELVETICA, 8.5f, Muted))
        {
            Alignment = Element.ALIGN_RIGHT,
        });

        header.AddCell(titleCell);
        doc.Add(header);

        var rule = new PdfPTable(1) { WidthPercentage = 100f, SpacingBefore = 4f, SpacingAfter = 6f };
        rule.AddCell(new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            BackgroundColor = BrandAccent,
            FixedHeight = 2f,
        });
        doc.Add(rule);
    }

    private static void AddSummaryLine(Document doc, int productCount, int totalUnits)
    {
        var text = productCount == 0
            ? "Products to buy: 0   ·   Units needed: 0"
            : $"Products to buy: {productCount}   ·   Units needed: {totalUnits}";
        doc.Add(new Paragraph(text, FontFactory.GetFont(FontFactory.HELVETICA, 9f, Muted))
        {
            SpacingAfter = 8f,
        });
    }

    private static void AddSectionLabel(Document doc, string text, BaseColor fg, BaseColor bg)
    {
        var table = new PdfPTable(1) { WidthPercentage = 100f, SpacingBefore = 2f, SpacingAfter = 3f };
        table.AddCell(new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, fg)))
        {
            BackgroundColor = bg,
            Border = Rectangle.NO_BORDER,
            PaddingTop = 3.5f,
            PaddingBottom = 3.5f,
            PaddingLeft = 6f,
        });
        doc.Add(table);
    }

    private static void AddCompactTable(Document doc, IReadOnlyList<StockShoppingListRowViewModel> rows)
    {
        var table = new PdfPTable(4) { WidthPercentage = 100f, SpacingAfter = 6f };
        table.SetWidths(new float[] { 4.6f, 1.1f, 1.3f, 1.3f });
        table.HeaderRows = 1;

        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8f, White);
        AddTh(table, "PRODUCT", headerFont, Element.ALIGN_LEFT);
        AddTh(table, "IN STOCK", headerFont, Element.ALIGN_CENTER);
        AddTh(table, "UNITS TO BUY", headerFont, Element.ALIGN_CENTER);
        AddTh(table, "PACKS TO BUY", headerFont, Element.ALIGN_CENTER);

        var nameFont = FontFactory.GetFont(FontFactory.HELVETICA, 8.5f, TextPrimary);
        var qtyFont = FontFactory.GetFont(FontFactory.HELVETICA, 8.5f, TextPrimary);
        var buyFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, TextPrimary);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var bg = i % 2 == 0 ? White : ZebraRow;
            AddTd(table, row.Name, nameFont, bg, Element.ALIGN_LEFT);
            AddTd(table, row.HaveQty.ToString(CultureInfo.InvariantCulture), qtyFont, bg, Element.ALIGN_CENTER);
            AddTd(table, row.UnitsToBuy.ToString(CultureInfo.InvariantCulture), buyFont, bg, Element.ALIGN_CENTER);
            var packs = row.PacksToBuy is int p ? p.ToString(CultureInfo.InvariantCulture) : "—";
            AddTd(table, packs, qtyFont, bg, Element.ALIGN_CENTER);
        }

        doc.Add(table);
    }

    private static void AddTh(PdfPTable table, string text, PdfFont font, int align)
    {
        table.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = HeaderBg,
            BorderColor = HeaderBg,
            HorizontalAlignment = align,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 4f,
            PaddingBottom = 4f,
            PaddingLeft = 5f,
            PaddingRight = 5f,
        });
    }

    private static void AddTd(PdfPTable table, string text, PdfFont font, BaseColor bg, int align)
    {
        table.AddCell(new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = bg,
            BorderColor = BorderColor,
            BorderWidth = 0.4f,
            HorizontalAlignment = align,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 3f,
            PaddingBottom = 3f,
            PaddingLeft = 5f,
            PaddingRight = 5f,
        });
    }

    private static void AddFooterNotes(
        Document doc,
        IReadOnlyList<string> missingPackSizeNames,
        IReadOnlyList<string> invalidReorderNames)
    {
        var noteFont = FontFactory.GetFont(FontFactory.HELVETICA, 7.5f, Muted);
        var warnFont = FontFactory.GetFont(FontFactory.HELVETICA, 7.5f, WarnFg);

        if (missingPackSizeNames.Count > 0)
        {
            doc.Add(new Paragraph(
                "Pack size needed: " + string.Join(", ", missingPackSizeNames),
                warnFont)
            {
                SpacingBefore = 4f,
                SpacingAfter = 2f,
            });
        }

        if (invalidReorderNames.Count > 0)
        {
            doc.Add(new Paragraph(
                "Review reorder settings (no target/warn): " + string.Join(", ", invalidReorderNames),
                warnFont)
            {
                SpacingBefore = 2f,
                SpacingAfter = 2f,
            });
        }

        doc.Add(new Paragraph(
            $"Generated by {ClubName} POS · {DateTime.Now.ToString("d MMM yyyy h:mm tt", Culture)}",
            noteFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingBefore = 8f,
        });
    }
}

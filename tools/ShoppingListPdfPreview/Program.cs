using System.Globalization;
using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfFont = iTextSharp.text.Font;
using PdfImage = iTextSharp.text.Image;

// Standalone preview generator mirroring NickeltownPOSV4.Services.Stock.ShoppingListPdfBuilder.
// Used on Linux CI agents where the WinUI app cannot be built.

var outDir = args.Length > 0 ? args[0] : Path.Combine("artifacts", "shopping-list-pdfs");
Directory.CreateDirectory(outDir);

var barRows = new List<PreviewRow>
{
    new("Great Northern Original", 0, 24, 2, BuyBucket.Now),
    new("XXXX Gold Stubbies", 2, 22, 2, BuyBucket.Now),
    new("Carlton Dry", 4, 20, 2, BuyBucket.Now),
    new("Bundaberg Rum Premix", 1, 11, 1, BuyBucket.Now),
    new("Jim Beam & Cola", 3, 9, 1, BuyBucket.Now),
    new("Coca-Cola 375ml", 6, 18, 1, BuyBucket.Now),
    new("Diet Coke 375ml", 8, 16, 1, BuyBucket.Soon),
    new("Mount Franklin Water", 10, 14, 1, BuyBucket.Soon),
    new("Lemonade 1.25L", 5, 7, 1, BuyBucket.Soon),
    new("Schweppes Lemonade", 4, 8, null, BuyBucket.Soon),
};

var merchRows = new List<PreviewRow>
{
    new("Club Hoodie — Navy M", 1, 3, 3, BuyBucket.Now),
    new("Club Tee — White L", 2, 4, 4, BuyBucket.Now),
    new("Stubby Holder", 4, 8, 1, BuyBucket.Soon),
    new("Club Banner", 0, 1, 1, BuyBucket.Now),
    new("Club Stickers (pack)", 3, 9, null, BuyBucket.Soon),
};

var barPath = Path.Combine(outDir, "Weekly_Bar_Shopping_List_preview.pdf");
var merchPath = Path.Combine(outDir, "Merchandise_Shopping_List_preview.pdf");
File.WriteAllBytes(barPath, PreviewPdfBuilder.Build("Weekly Bar Shopping List", barRows, ["Schweppes Lemonade"], []));
File.WriteAllBytes(merchPath, PreviewPdfBuilder.Build("Merchandise Shopping List", merchRows, ["Club Stickers (pack)"], []));

Console.WriteLine("Wrote " + barPath);
Console.WriteLine("Wrote " + merchPath);

enum BuyBucket { Now, Soon }

sealed record PreviewRow(string Name, int InStock, int UnitsToBuy, int? PacksToBuy, BuyBucket Bucket);

static class PreviewPdfBuilder
{
    private const string ClubName = "Nickeltown Flounderers";
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");
    private static readonly BaseColor BrandPrimary = new(30, 58, 138);
    private static readonly BaseColor BrandAccent = new(59, 130, 246);
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

    public static byte[] Build(
        string title,
        IReadOnlyList<PreviewRow> purchaseRows,
        IReadOnlyList<string> missingPackSizeNames,
        IReadOnlyList<string> invalidReorderNames)
    {
        var buyNow = purchaseRows.Where(r => r.Bucket == BuyBucket.Now).ToList();
        var buySoon = purchaseRows.Where(r => r.Bucket == BuyBucket.Soon).ToList();
        var totalUnits = purchaseRows.Sum(r => r.UnitsToBuy);

        using var ms = new MemoryStream();
        using (var doc = new Document(PageSize.A4, 36f, 36f, 28f, 28f))
        {
            PdfWriter.GetInstance(doc, ms);
            doc.Open();
            AddCompactHeader(doc, title);
            AddSummaryLine(doc, purchaseRows.Count, totalUnits);

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

            AddFooterNotes(doc, missingPackSizeNames, invalidReorderNames);
        }

        return ms.ToArray();
    }

    private static void AddCompactHeader(Document doc, string title)
    {
        var header = new PdfPTable(2) { WidthPercentage = 100f };
        header.SetWidths(new float[] { 0.9f, 3.1f });
        header.SpacingAfter = 2f;
        header.AddCell(CreateLogoCell());

        var titleCell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            Padding = 2f,
        };
        titleCell.AddElement(new Paragraph(ClubName, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10f, BrandPrimary))
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 1f,
        });
        titleCell.AddElement(new Paragraph(title, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14f, TextPrimary))
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 1f,
        });
        titleCell.AddElement(new Paragraph(DateTime.Now.ToString("dddd d MMMM yyyy", Culture), FontFactory.GetFont(FontFactory.HELVETICA, 8.5f, Muted))
        {
            Alignment = Element.ALIGN_RIGHT,
        });
        header.AddCell(titleCell);
        doc.Add(header);

        var rule = new PdfPTable(1) { WidthPercentage = 100f, SpacingBefore = 4f, SpacingAfter = 6f };
        rule.AddCell(new PdfPCell { Border = Rectangle.NO_BORDER, BackgroundColor = BrandAccent, FixedHeight = 2f });
        doc.Add(rule);
    }

    private static PdfPCell CreateLogoCell()
    {
        var cell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_LEFT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
        };
        var logo = FindLogo();
        if (logo is not null)
        {
            try
            {
                var img = PdfImage.GetInstance(logo);
                img.ScaleToFit(52f, 40f);
                cell.AddElement(img);
            }
            catch
            {
                // ignore
            }
        }

        return cell;
    }

    private static string? FindLogo()
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "FlounderersLogo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "NickeltownPOSV4", "Assets", "FlounderersLogo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "FlounderersLogo.png"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static void AddSummaryLine(Document doc, int productCount, int totalUnits)
    {
        doc.Add(new Paragraph(
            $"Products to buy: {productCount}   ·   Units needed: {totalUnits}",
            FontFactory.GetFont(FontFactory.HELVETICA, 9f, Muted))
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

    private static void AddCompactTable(Document doc, IReadOnlyList<PreviewRow> rows)
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
            AddTd(table, row.InStock.ToString(CultureInfo.InvariantCulture), qtyFont, bg, Element.ALIGN_CENTER);
            AddTd(table, row.UnitsToBuy.ToString(CultureInfo.InvariantCulture), buyFont, bg, Element.ALIGN_CENTER);
            AddTd(table, row.PacksToBuy?.ToString(CultureInfo.InvariantCulture) ?? "—", qtyFont, bg, Element.ALIGN_CENTER);
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
            doc.Add(new Paragraph("Pack size needed: " + string.Join(", ", missingPackSizeNames), warnFont)
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

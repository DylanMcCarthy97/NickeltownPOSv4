using System;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Pitstop;

public static class PitstopReportPdfExporter
{
    private const string ClubName = "Nickeltown Flounderers";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    private static readonly BaseColor BrandPrimary = new(30, 58, 138);

    private static readonly BaseColor Muted = new(107, 114, 128);

    private static readonly BaseColor TextPrimary = new(17, 24, 39);

    private static readonly BaseColor BorderColor = new(229, 231, 235);

    private static readonly BaseColor White = new(255, 255, 255);

    public static byte[] Build(PitstopReportData d)
    {
        using var ms = new MemoryStream();
        using (var doc = new Document(PageSize.A4, 40, 40, 40, 48))
        {
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            AddHeader(doc, d);
            AddSummaryTable(doc, d);
            AddOutsideTable(doc, d);
            AddReconciliation(doc, d);
            AddCategoryComparison(doc, d);
            AddCashCount(doc, d);
            AddExpensesAndFloats(doc, d);
            AddPitstopSales(doc, d);
            AddWarnings(doc, d);
            AddFooter(doc);
            doc.Close();
        }

        return ms.ToArray();
    }

    private static void AddHeader(Document doc, PitstopReportData d)
    {
        var header = new PdfPTable(2) { WidthPercentage = 100 };
        header.SetWidths(new float[] { 1.1f, 3f });
        header.SpacingAfter = 6;

        header.AddCell(ReportPdfBranding.CreateLogoCell());

        var titleCell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            Padding = 3,
        };

        var small = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BrandPrimary);
        titleCell.AddElement(new Paragraph(ClubName.ToUpperInvariant(), small)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 2,
        });

        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, TextPrimary);
        var titleText = d.IsTestReport ? "Pitstop end-of-day report (TEST)" : "Pitstop end-of-day report";
        titleCell.AddElement(new Paragraph(titleText, titleFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 4,
        });

        if (d.IsTestReport)
        {
            var testBanner = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(185, 28, 28));
            titleCell.AddElement(new Paragraph("SAMPLE DATA — NOT A REAL EVENT", testBanner)
            {
                Alignment = Element.ALIGN_RIGHT,
                SpacingAfter = 4,
            });
        }

        var body = FontFactory.GetFont(FontFactory.HELVETICA, 10, TextPrimary);
        titleCell.AddElement(new Paragraph(
            string.IsNullOrWhiteSpace(d.EventName) ? "(No event name)" : d.EventName,
            body)
        {
            Alignment = Element.ALIGN_RIGHT,
        });
        titleCell.AddElement(new Paragraph(d.PeriodCaption, FontFactory.GetFont(FontFactory.HELVETICA, 9, Muted))
        {
            Alignment = Element.ALIGN_RIGHT,
        });
        titleCell.AddElement(new Paragraph("Pitstop side only — bar tabs excluded", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, Muted))
        {
            Alignment = Element.ALIGN_RIGHT,
        });
        if (!string.IsNullOrWhiteSpace(d.StaffName))
        {
            titleCell.AddElement(new Paragraph($"Staff: {d.StaffName}", body) { Alignment = Element.ALIGN_RIGHT });
        }

        header.AddCell(titleCell);
        doc.Add(header);
    }

    private static void AddSummaryTable(Document doc, PitstopReportData d)
    {
        doc.Add(new Paragraph("Financial summary (Pitstop)", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        var t = NewKeyValueTable();
        AddKv(t, "Pitstop terminal cash", d.PitstopRetailCash);
        AddKv(t, "Pitstop terminal card (Square)", d.PitstopRetailCard);
        AddKv(t, "Pitstop card product base", d.PitstopCardBaseProductTotal);
        AddKv(t, "Pitstop card surcharge", d.PitstopCardSurchargeCollected);
        AddKv(t, "Outside cash (merch + raffle)", d.OutsideCashTotal);
        AddKv(t, "Outside card (Square)", d.OutsideSquareGross);
        AddKv(t, "Combined Square card gross", d.CombinedSquareCardGross);
        AddKv(t, "Total cash counted", d.TotalCashGross);
        AddKv(t, "Gross Pitstop sales", d.GrossSales);
        AddKv(t, "Total expenses", d.TotalExpenses);
        AddKv(t, $"Est. Square fees ({d.SquareFeePercent:0.##}%)", d.EstimatedSquareFees);
        AddKv(t, "Cash to deposit (after float)", d.CashToDeposit);
        AddKv(t, "Net event profit", d.NetEventProfit);
        doc.Add(t);
        if (d.UsingManualSquareCardFallback)
        {
            doc.Add(new Paragraph(
                "Manual Square card fallback was used — outside card was derived as total Square card minus POS terminal card.",
                FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, Muted))
            {
                SpacingAfter = 6,
            });
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 6 });
    }

    private static void AddOutsideTable(Document doc, PitstopReportData d)
    {
        doc.Add(new Paragraph("Outside sales — manual cash + Square card", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        var table = new PdfPTable(5) { WidthPercentage = 100 };
        table.SetWidths(new float[] { 2.4f, 0.9f, 1.1f, 0.9f, 1.1f });
        AddH(table, "Product");
        AddH(table, "Cash qty");
        AddH(table, "Cash $");
        AddH(table, "Square qty");
        AddH(table, "Square $");
        foreach (var r in d.CombinedOutsideSales)
        {
            AddC(table, r.Name);
            AddC(table, r.CashQuantity.ToString(Culture));
            AddC(table, r.CashTotal.ToString("0.00", Culture));
            AddC(table, r.CardQuantity.ToString(Culture));
            AddC(table, r.CardTotal.ToString("0.00", Culture));
        }

        doc.Add(table);

        if (d.SquareUnmatchedPayments.Count > 0)
        {
            doc.Add(new Paragraph("Outside Square transactions", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, TextPrimary)) { SpacingBefore = 6, SpacingAfter = 4 });
            foreach (var payment in d.SquareUnmatchedPayments.OrderBy(x => x.PaidAt).Take(20))
            {
                var receipt = string.IsNullOrWhiteSpace(payment.ReceiptNumber) ? "—" : payment.ReceiptNumber;
                doc.Add(new Paragraph(
                    $"{payment.PaidAt.LocalDateTime:HH:mm}  receipt {receipt}  total {payment.GrossAmount:0.00}",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, TextPrimary)));
                foreach (var line in payment.LineItems)
                {
                    doc.Add(new Paragraph(
                        $"  {line.ItemName} x{line.Quantity}  {line.LineTotal:0.00}",
                        FontFactory.GetFont(FontFactory.HELVETICA, 8, TextPrimary)));
                }
            }
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 6 });
    }

    private static void AddReconciliation(Document doc, PitstopReportData d)
    {
        doc.Add(new Paragraph("Square reconciliation", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        var t = NewKeyValueTable();
        AddKv(t, "POS terminal (Square Terminal 0070) — transactions", d.PosSquareTransactionCount);
        AddKv(t, "POS terminal card gross", d.PosSquareGross);
        AddKv(t, "Outside terminal (Flounderers02) — transactions", d.OutsideSquareTransactionCount);
        AddKv(t, "Outside terminal card gross", d.OutsideSquareGross);
        AddKv(t, "Combined Square card gross", d.CombinedSquareCardGross);
        if (d.ActualSquareFees is decimal fees)
        {
            AddKv(t, "Square processing fees", fees);
        }
        else
        {
            AddKv(t, $"Est. Square fees ({d.SquareFeePercent:0.##}%)", d.EstimatedSquareFees);
        }

        AddKv(t, "Expected Square deposit", d.ExpectedSquareDeposit);
        AddKv(t, "Pitstop terminal card (POS DB)", d.PitstopRetailCard);
        AddKv(t, "Difference (Square POS − POS DB)", d.OutsideCardDifference);
        doc.Add(t);

        if (d.OutsideCardMismatch)
        {
            doc.Add(new Paragraph(
                "Warning: Square POS total does not match Pitstop terminal card total from POS.",
                FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, new BaseColor(185, 28, 28))));
        }

        if (d.SquareUnmatchedPayments.Count > 0)
        {
            doc.Add(new Paragraph("Outside terminal payments (not in ClubPOS)", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, TextPrimary)) { SpacingBefore = 6, SpacingAfter = 4 });
            var ut = new PdfPTable(4) { WidthPercentage = 100 };
            ut.SetWidths(new float[] { 1.1f, 0.8f, 1f, 1.4f });
            AddH(ut, "Time");
            AddH(ut, "Amount");
            AddH(ut, "Receipt");
            AddH(ut, "Device");
            foreach (var p in d.SquareUnmatchedPayments.Take(20))
            {
                AddC(ut, p.PaidAt.LocalDateTime.ToString("HH:mm", Culture));
                AddC(ut, p.GrossAmount.ToString("0.00", Culture));
                AddC(ut, string.IsNullOrWhiteSpace(p.ReceiptNumber) ? "—" : p.ReceiptNumber);
                AddC(ut, string.IsNullOrWhiteSpace(p.DeviceName) ? "—" : p.DeviceName);
            }

            doc.Add(ut);
        }

        if (d.SquareMissingLocalPayments.Count > 0)
        {
            doc.Add(new Paragraph("Missing Square payments", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(185, 28, 28))) { SpacingBefore = 6, SpacingAfter = 4 });
            var mt = new PdfPTable(3) { WidthPercentage = 100 };
            mt.SetWidths(new float[] { 1.6f, 0.8f, 2f });
            AddH(mt, "Sale");
            AddH(mt, "Amount");
            AddH(mt, "PaymentId");
            foreach (var m in d.SquareMissingLocalPayments.Take(20))
            {
                AddC(mt, m.SaleRef);
                AddC(mt, m.Amount.ToString("0.00", Culture));
                AddC(mt, m.PaymentId);
            }

            doc.Add(mt);
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 6 });
    }

    private static void AddOutsideTerminalSales(Document doc, PitstopReportData d)
    {
        doc.Add(new Paragraph("Outside terminal — itemised sales (Square)", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        if (d.OutsideTerminalProductSales.Count == 0)
        {
            doc.Add(new Paragraph("No outside-terminal line items loaded.", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, Muted)));
        }
        else
        {
            var pt = new PdfPTable(4) { WidthPercentage = 100 };
            pt.SetWidths(new float[] { 2.2f, 1.4f, 0.7f, 0.9f });
            AddH(pt, "Product");
            AddH(pt, "Category");
            AddH(pt, "Qty");
            AddH(pt, "Total");
            foreach (var p in d.OutsideTerminalProductSales.Take(30))
            {
                AddC(pt, p.Name);
                AddC(pt, p.CategoryName);
                AddC(pt, p.Quantity.ToString(Culture));
                AddC(pt, p.LineTotal.ToString("0.00", Culture));
            }

            doc.Add(pt);
        }

        if (d.SquareUnmatchedPayments.Count > 0)
        {
            doc.Add(new Paragraph("Outside terminal transactions", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, TextPrimary)) { SpacingBefore = 6, SpacingAfter = 4 });
            foreach (var p in d.SquareUnmatchedPayments.OrderBy(x => x.PaidAt).Take(20))
            {
                var receipt = string.IsNullOrWhiteSpace(p.ReceiptNumber) ? "—" : p.ReceiptNumber;
                var device = string.IsNullOrWhiteSpace(p.DeviceName) ? "—" : p.DeviceName;
                doc.Add(new Paragraph(
                    $"{p.PaidAt.LocalDateTime:HH:mm}  rcpt {receipt}  {p.GrossAmount:0.00}  {device}",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, TextPrimary))
                { SpacingAfter = 2 });

                if (p.LineItems.Count == 0)
                {
                    doc.Add(new Paragraph(
                        string.IsNullOrWhiteSpace(p.OrderLoadWarning) ? "No line items loaded." : p.OrderLoadWarning!,
                        FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, Muted)));
                }
                else
                {
                    foreach (var line in p.LineItems)
                    {
                        doc.Add(new Paragraph(
                            $"  {line.ItemName} x{line.Quantity}  {line.LineTotal:0.00}",
                            FontFactory.GetFont(FontFactory.HELVETICA, 8, TextPrimary)));
                    }
                }
            }
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 6 });
    }

    private static void AddCategoryComparison(Document doc, PitstopReportData d)
    {
        if (d.EventCategoryComparison.Count == 0)
        {
            return;
        }

        doc.Add(new Paragraph("Event sales by category", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        var ct = new PdfPTable(7) { WidthPercentage = 100 };
        ct.SetWidths(new float[] { 1.4f, 0.7f, 0.9f, 0.7f, 0.9f, 0.7f, 0.9f });
        AddH(ct, "Category");
        AddH(ct, "POS qty");
        AddH(ct, "POS $");
        AddH(ct, "Outside qty");
        AddH(ct, "Outside $");
        AddH(ct, "Combined qty");
        AddH(ct, "Combined $");
        foreach (var row in d.EventCategoryComparison)
        {
            AddC(ct, row.CategoryName);
            AddC(ct, row.ClubPosQuantity.ToString(Culture));
            AddC(ct, row.ClubPosLineTotal.ToString("0.00", Culture));
            AddC(ct, row.OutsideTerminalQuantity.ToString(Culture));
            AddC(ct, row.OutsideTerminalLineTotal.ToString("0.00", Culture));
            AddC(ct, row.CombinedQuantity.ToString(Culture));
            AddC(ct, row.CombinedLineTotal.ToString("0.00", Culture));
        }

        doc.Add(ct);
        doc.Add(new Paragraph(" ") { SpacingAfter = 6 });
    }

    private static void AddKv(PdfPTable t, string k, int v)
    {
        AddC(t, k);
        AddC(t, v.ToString(Culture));
    }

    private static void AddExpensesAndFloats(Document doc, PitstopReportData d)
    {
        doc.Add(new Paragraph("Expenses", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        if (d.Expenses.Count == 0)
        {
            doc.Add(new Paragraph("None", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, Muted)));
        }
        else
        {
            var et = new PdfPTable(2) { WidthPercentage = 100 };
            et.SetWidths(new float[] { 3f, 1f });
            AddH(et, "Description");
            AddH(et, "Amount");
            foreach (var e in d.Expenses)
            {
                AddC(et, string.IsNullOrWhiteSpace(e.Description) ? "—" : e.Description);
                AddC(et, e.Amount.ToString("0.00", Culture));
            }

            doc.Add(et);
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 4 });
        doc.Add(new Paragraph("Floats", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        var ft = NewKeyValueTable();
        AddKv(ft, "Inside float (terminal till)", d.InsideFloat);
        AddKv(ft, "Outside float (merch table)", d.OutsideFloat);
        doc.Add(ft);

        doc.Add(new Paragraph(" ") { SpacingAfter = 4 });
        doc.Add(new Paragraph("Prize giveaways (qty)", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        if (d.PrizeGiveaways.Count == 0)
        {
            doc.Add(new Paragraph("None", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, Muted)));
        }
        else
        {
            var gt = new PdfPTable(2) { WidthPercentage = 100 };
            gt.SetWidths(new float[] { 3f, 0.8f });
            AddH(gt, "Item");
            AddH(gt, "Qty");
            foreach (var g in d.PrizeGiveaways)
            {
                AddC(gt, g.ItemName);
                AddC(gt, g.Quantity.ToString(Culture));
            }

            doc.Add(gt);
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 6 });
    }

    private static void AddPitstopSales(Document doc, PitstopReportData d)
    {
        doc.Add(new Paragraph("Pitstop terminal — itemised sales", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        if (d.PitstopProductSales.Count == 0)
        {
            doc.Add(new Paragraph("No Pitstop terminal sales in range.", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, Muted)));
        }
        else
        {
            var pt = new PdfPTable(4) { WidthPercentage = 100 };
            pt.SetWidths(new float[] { 2.2f, 1.4f, 0.7f, 0.9f });
            AddH(pt, "Product");
            AddH(pt, "Category");
            AddH(pt, "Qty");
            AddH(pt, "Total");
            foreach (var p in d.PitstopProductSales.Take(24))
            {
                AddC(pt, p.Name);
                AddC(pt, p.CategoryName);
                AddC(pt, p.Quantity.ToString(Culture));
                AddC(pt, p.LineTotal.ToString("0.00", Culture));
            }

            if (d.PitstopProductSales.Count > 24)
            {
                AddC(pt, $"… plus {d.PitstopProductSales.Count - 24} more", colspan: 4);
            }

            doc.Add(pt);
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 4 });
        doc.Add(new Paragraph("Pitstop payment mix", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, TextPrimary)) { SpacingAfter = 2 });
        if (d.PitstopPaymentBreakdown.Count == 0)
        {
            doc.Add(new Paragraph("—", FontFactory.GetFont(FontFactory.HELVETICA, 9, Muted)));
        }
        else
        {
            var bt = new PdfPTable(2) { WidthPercentage = 60 };
            bt.SetWidths(new float[] { 1.2f, 1f });
            foreach (var b in d.PitstopPaymentBreakdown)
            {
                AddC(bt, b.PaymentMethod);
                AddC(bt, b.Total.ToString("0.00", Culture));
            }

            doc.Add(bt);
        }
    }

    private static void AddCashCount(Document doc, PitstopReportData d)
    {
        if (d.CashCounted is null && d.FloatRemoved is null && d.InsideFloat <= 0m)
        {
            return;
        }

        doc.Add(new Paragraph("Cash count", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, TextPrimary)) { SpacingAfter = 4 });
        var t = NewKeyValueTable();
        AddKv(t, "Starting float (inside till)", d.InsideFloat);
        AddKv(t, "Pitstop cash sales", d.PitstopRetailCash);
        if (d.ExpectedCash is decimal exp)
        {
            AddKv(t, "Expected cash (float + cash sales)", exp);
        }

        if (d.CashCounted is decimal cnt)
        {
            AddKv(t, "Cash counted at EOD", cnt);
        }

        if (d.FloatRemoved is decimal flt)
        {
            AddKv(t, "Float removed", flt);
        }

        if (d.CashVariance is decimal v)
        {
            AddKv(t, "Cash variance (counted − expected)", v);
        }

        doc.Add(t);

        if (d.CashVariance is decimal var2 && Math.Abs(var2) >= 0.01m)
        {
            var msg = var2 > 0
                ? $"Warning: cash drawer is over by {Math.Abs(var2):0.00}."
                : $"Warning: cash drawer is short by {Math.Abs(var2):0.00}.";
            doc.Add(new Paragraph(msg, FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, new BaseColor(185, 28, 28))));
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 6 });
    }

    private static void AddWarnings(Document doc, PitstopReportData d)
    {
        if (d.Warnings.Count == 0)
        {
            return;
        }

        doc.Add(new Paragraph("Warnings", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(180, 83, 9))) { SpacingAfter = 4 });
        var warnFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(180, 83, 9));
        foreach (var w in d.Warnings)
        {
            if (string.IsNullOrWhiteSpace(w))
            {
                continue;
            }

            doc.Add(new Paragraph("• " + w, warnFont) { SpacingAfter = 2 });
        }

        doc.Add(new Paragraph(" ") { SpacingAfter = 6 });
    }

    private static void AddFooter(Document doc)
    {
        doc.Add(new Paragraph(" ") { SpacingBefore = 8 });
        var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz", Culture);
        doc.Add(new Paragraph($"Generated {stamp}", FontFactory.GetFont(FontFactory.HELVETICA, 8, Muted)));
    }

    private static PdfPTable NewKeyValueTable()
    {
        var t = new PdfPTable(2) { WidthPercentage = 72 };
        t.SetWidths(new float[] { 2.4f, 1f });
        return t;
    }

    private static void AddKv(PdfPTable t, string k, decimal v)
    {
        AddC(t, k);
        AddC(t, v.ToString("0.00", Culture));
    }

    private static void AddH(PdfPTable t, string text)
    {
        var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, White)))
        {
            BackgroundColor = BrandPrimary,
            BorderColor = BorderColor,
            Padding = 5,
        };
        t.AddCell(cell);
    }

    private static void AddC(PdfPTable t, string text, int colspan = 1)
    {
        var cell = new PdfPCell(new Phrase(text ?? string.Empty, FontFactory.GetFont(FontFactory.HELVETICA, 8, TextPrimary)))
        {
            BorderColor = BorderColor,
            Padding = 4,
            Colspan = colspan,
        };
        t.AddCell(cell);
    }
}

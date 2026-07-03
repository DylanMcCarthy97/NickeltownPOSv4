using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Settings;

/// <summary>Single-tab ledger export (A4 portrait) for a chosen date range.</summary>
internal static class TabHistoryPdfBuilder
{
    private const string ClubName = "Nickeltown Flounderers";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    private static readonly BaseColor BrandPrimary = new(30, 58, 138);
    private static readonly BaseColor Muted = new(107, 114, 128);
    private static readonly BaseColor TextPrimary = new(17, 24, 39);
    private static readonly BaseColor BorderColor = new(229, 231, 235);
    private static readonly BaseColor ZebraRow = new(249, 250, 251);
    private static readonly BaseColor White = new(255, 255, 255);

    /// <summary>V2-style export: Date, Type, Method, Amount, Bartender, Details.</summary>
    public static byte[] BuildV2Style(string tabDisplayName, string rangeCaption, IReadOnlyList<TabHistoryEntryRow> rows) =>
        Build(tabDisplayName, rangeCaption, rows, v2Columns: true);

    public static byte[] Build(string tabDisplayName, string rangeCaption, IReadOnlyList<TabHistoryEntryRow> rows) =>
        Build(tabDisplayName, rangeCaption, rows, v2Columns: false);

    private static byte[] Build(string tabDisplayName, string rangeCaption, IReadOnlyList<TabHistoryEntryRow> rows, bool v2Columns)
    {
        var title = "Tab history";
        var safeTab = string.IsNullOrWhiteSpace(tabDisplayName) ? "Tab" : tabDisplayName.Trim();

        using var ms = new MemoryStream();
        using (var doc = new Document(PageSize.A4, 36, 36, 36, 36))
        {
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            AddHeader(doc, title, safeTab, rangeCaption);

            if (rows.Count == 0)
            {
                var emptyFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 11, Muted);
                doc.Add(new Paragraph("No lines in this range.", emptyFont));
            }
            else if (v2Columns)
            {
                var table = new PdfPTable(6) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 2f, 1.6f, 1.4f, 1f, 1.2f, 2.8f });
                table.HeaderRows = 1;

                void AddHeaderCell(string text)
                {
                    var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, White)))
                    {
                        BackgroundColor = BrandPrimary,
                        BorderColor = BorderColor,
                        Padding = 6,
                    };
                    table.AddCell(cell);
                }

                AddHeaderCell("Date");
                AddHeaderCell("Type");
                AddHeaderCell("Method");
                AddHeaderCell("Amount");
                AddHeaderCell("Bartender");
                AddHeaderCell("Details");

                for (var i = 0; i < rows.Count; i++)
                {
                    var line = TabHistoryEntryDisplayFormatter.ParseLedgerLine(rows[i]);
                    var zebra = i % 2 == 1 ? ZebraRow : White;
                    var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, TextPrimary);

                    void AddBodyCell(string text, int horizontalAlign)
                    {
                        var cell = new PdfPCell(new Phrase(text ?? string.Empty, bodyFont))
                        {
                            BackgroundColor = zebra,
                            BorderColor = BorderColor,
                            Padding = 5,
                            HorizontalAlignment = horizontalAlign,
                        };
                        table.AddCell(cell);
                    }

                    AddBodyCell(line.WhenDisplay, Element.ALIGN_LEFT);
                    AddBodyCell(line.Type, Element.ALIGN_LEFT);
                    AddBodyCell(line.PaymentMethod, Element.ALIGN_LEFT);
                    AddBodyCell(line.AmountDisplay, Element.ALIGN_RIGHT);
                    AddBodyCell(line.Bartender, Element.ALIGN_LEFT);
                    AddBodyCell(line.Details, Element.ALIGN_LEFT);
                }

                doc.Add(table);
            }
            else
            {
                var table = new PdfPTable(4) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 2.2f, 1.6f, 1.1f, 3.1f });
                table.HeaderRows = 1;

                void AddHeaderCell(string text)
                {
                    var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, White)))
                    {
                        BackgroundColor = BrandPrimary,
                        BorderColor = BorderColor,
                        Padding = 6,
                    };
                    table.AddCell(cell);
                }

                AddHeaderCell("When (local)");
                AddHeaderCell("Type");
                AddHeaderCell("Amount");
                AddHeaderCell("Note");

                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var zebra = i % 2 == 1 ? ZebraRow : White;
                    var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, TextPrimary);

                    void AddBodyCell(string text, int horizontalAlign)
                    {
                        var cell = new PdfPCell(new Phrase(text ?? string.Empty, bodyFont))
                        {
                            BackgroundColor = zebra,
                            BorderColor = BorderColor,
                            Padding = 5,
                            HorizontalAlignment = horizontalAlign,
                        };
                        table.AddCell(cell);
                    }

                    var whenRaw = string.IsNullOrWhiteSpace(r.OccurredAt) ? r.CreatedAt : r.OccurredAt;
                    AddBodyCell(TabHistoryEntryDisplayFormatter.FormatWhenForDisplay(whenRaw), Element.ALIGN_LEFT);
                    AddBodyCell(string.IsNullOrWhiteSpace(r.EntryType) ? "—" : r.EntryType.Trim(), Element.ALIGN_LEFT);
                    var amt = r.Amount is { } a ? a.ToString("0.00", Culture) : "—";
                    AddBodyCell(amt, Element.ALIGN_RIGHT);
                    AddBodyCell(TabHistoryEntryDisplayFormatter.FormatPdfNoteColumn(r), Element.ALIGN_LEFT);
                }

                doc.Add(table);
            }

            var footer = FontFactory.GetFont(FontFactory.HELVETICA, 8, Muted);
            doc.Add(
                new Paragraph(
                    $"Generated {DateTimeOffset.Now.ToLocalTime().ToString("g", Culture)}",
                    footer)
                {
                    SpacingBefore = 14,
                });
        }

        return ms.ToArray();
    }

    private static void AddHeader(Document doc, string title, string tabName, string rangeCaption)
    {
        var header = new PdfPTable(2) { WidthPercentage = 100 };
        header.SetWidths(new float[] { 1.1f, 3f });
        header.SpacingAfter = 10;

        header.AddCell(ReportPdfBranding.CreateLogoCell());

        var titleCell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            Padding = 3,
        };

        var clubFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BrandPrimary);
        titleCell.AddElement(new Paragraph(ClubName.ToUpperInvariant(), clubFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 2,
        });

        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, TextPrimary);
        titleCell.AddElement(new Paragraph(title, titleFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 4,
        });

        var subFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, TextPrimary);
        titleCell.AddElement(new Paragraph(tabName, subFont)
        {
            Alignment = Element.ALIGN_RIGHT,
            SpacingAfter = 2,
        });

        var rangeFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, Muted);
        titleCell.AddElement(new Paragraph(rangeCaption, rangeFont) { Alignment = Element.ALIGN_RIGHT });

        header.AddCell(titleCell);
        doc.Add(header);
    }
}

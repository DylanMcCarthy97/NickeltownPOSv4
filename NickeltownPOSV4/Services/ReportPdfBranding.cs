using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfImage = iTextSharp.text.Image;

namespace NickeltownPOSV4.Services;

/// <summary>Shared Flounderers logo resolution for PDF exports.</summary>
internal static class ReportPdfBranding
{
    public static string? FindLogoPath()
    {
        var baseDir = AppContext.BaseDirectory ?? string.Empty;
        string[] candidates =
        {
            Path.Combine(baseDir, "Assets", "FlounderersLogo.png"),
            Path.Combine(baseDir, "FlounderersLogo.png"),
            Path.Combine(baseDir, "Flounderers Logo.png"),
            Path.Combine(Environment.CurrentDirectory, "Assets", "FlounderersLogo.png"),
            Path.Combine(Environment.CurrentDirectory, "Flounderers Logo.png"),
        };

        foreach (var p in candidates)
        {
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
            {
                return p;
            }
        }

        return null;
    }

    public static void TryAddLogoToCell(PdfPCell logoCell, float maxWidth = 80f, float maxHeight = 80f)
    {
        var logoPath = FindLogoPath();
        if (string.IsNullOrEmpty(logoPath) || !File.Exists(logoPath))
        {
            return;
        }

        try
        {
            var img = PdfImage.GetInstance(logoPath);
            img.ScaleToFit(maxWidth, maxHeight);
            logoCell.AddElement(img);
        }
        catch
        {
            // Logo failed to load - keep the rest of the report rendering.
        }
    }

    public static PdfPCell CreateLogoCell(float maxWidth = 80f, float maxHeight = 80f)
    {
        var logoCell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_LEFT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            PaddingTop = 0,
            PaddingBottom = 0,
        };
        TryAddLogoToCell(logoCell, maxWidth, maxHeight);
        return logoCell;
    }
}

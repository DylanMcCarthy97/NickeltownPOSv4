using System;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public interface IReportExportService
{
    /// <summary>Builds a CSV of all tab activity (entries + money movements) for the calendar month containing <paramref name="month"/>.</summary>
    Task<byte[]> BuildMonthlyTabsCsvAsync(DateTime month, CancellationToken cancellationToken = default);

    /// <summary>Builds a single-page A4 PDF of the monthly bar tabs summary with the Flounderers logo and summary cards (modelled on POSBarV2).</summary>
    Task<byte[]> BuildMonthlyTabsPdfAsync(DateTime month, CancellationToken cancellationToken = default);

    /// <summary>Builds a CSV snapshot of the current items + stock counts.</summary>
    Task<byte[]> BuildStockSnapshotCsvAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds an A4 PDF stock snapshot with the Flounderers logo, summary cards, and a category-grouped table.</summary>
    Task<byte[]> BuildStockSnapshotPdfAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists archived tabs for the archived-tabs viewer (closed and/or marked-archived rows).</summary>
    Task<System.Collections.Generic.IReadOnlyList<ArchivedTabListRow>> ListArchivedTabsAsync(CancellationToken cancellationToken = default);
}

public sealed record ArchivedTabListRow(
    long Id,
    string DisplayName,
    string TabType,
    double Balance,
    DateTime? LastActivityAt,
    DateTime? ClosedAt,
    bool IsClosed,
    bool IsArchived);

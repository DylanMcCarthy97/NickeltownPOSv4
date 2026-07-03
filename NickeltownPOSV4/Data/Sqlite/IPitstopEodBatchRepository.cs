using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Data.Sqlite;

public interface IPitstopEodBatchRepository
{
    Task<int> GetActivePitstopSaleCountAsync(CancellationToken cancellationToken = default);

    Task<int> GetActivePitstopSaleCountForPeriodAsync(
        DateTimeOffset periodStartLocal,
        DateTimeOffset periodEndLocal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts rows in PitstopSales whose SaleMode is set to something other than 'pitstop'
    /// (e.g. an explicit 'tab' or other non-Pitstop tag). Used by System Check to verify that
    /// the Pitstop sales table only contains pitstop-area sales. NULL/empty SaleMode rows
    /// (legacy imports) are not counted here.
    /// </summary>
    Task<int> GetNonPitstopSaleModeCountAsync(CancellationToken cancellationToken = default);

    Task<PitstopEodArchiveResult> ArchiveActivePitstopSalesAsync(
        PitstopEodArchiveRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PitstopEodBatchListRow>> ListBatchesAsync(CancellationToken cancellationToken = default);

    Task<PitstopEodBatchDetail?> GetBatchDetailAsync(long batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PitstopArchivedSaleRow>> GetBatchSalesAsync(
        long batchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PitstopSaleLineReportRow>> GetBatchItemisedLinesAsync(
        long batchId,
        CancellationToken cancellationToken = default);

    Task<bool> AppendNoteAsync(long batchId, string note, CancellationToken cancellationToken = default);

    Task<bool> UpdateBackupAfterPathAsync(long batchId, string? backupAfterPath, CancellationToken cancellationToken = default);
}

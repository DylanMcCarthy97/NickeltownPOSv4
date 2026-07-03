using System;
using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Pitstop;

/// <summary>Summary row for the Previous Pitstops list.</summary>
public sealed class PitstopEodBatchListRow
{
    public long Id { get; init; }

    public DateTimeOffset ArchivedAt { get; init; }

    public string? EventName { get; init; }

    public decimal TotalSales { get; init; }

    public decimal CashTotal { get; init; }

    public decimal CardChargedTotal { get; init; }

    public decimal CardSurchargeTotal { get; init; }

    public decimal EstimatedSquareFees { get; init; }

    public decimal NetTotal { get; init; }

    public int SaleCount { get; init; }

    public string? OperatorName { get; init; }

    public string? PdfPath { get; init; }
}

/// <summary>Full archived batch with EOD snapshot for detail and reprint.</summary>
public sealed class PitstopEodBatchDetail
{
    public long Id { get; init; }

    public DateTimeOffset ArchivedAt { get; init; }

    public string? OperatorName { get; init; }

    public long? OperatorStaffId { get; init; }

    public string? EventName { get; init; }

    public DateTimeOffset? PeriodStartLocal { get; init; }

    public DateTimeOffset? PeriodEndLocal { get; init; }

    public decimal TotalSales { get; init; }

    public decimal CashTotal { get; init; }

    public decimal CardChargedTotal { get; init; }

    public decimal CardBaseProductTotal { get; init; }

    public decimal CardSurchargeTotal { get; init; }

    public decimal EstimatedSquareFees { get; init; }

    public decimal NetTotal { get; init; }

    public int SaleCount { get; init; }

    public string? PdfPath { get; init; }

    public PitstopReportData? ReportData { get; init; }

    public IReadOnlyList<string> ReconciliationWarnings { get; init; } = [];

    public string? Notes { get; init; }

    public decimal StartingFloat { get; init; }

    public decimal? CashCounted { get; init; }

    public decimal? FloatRemoved { get; init; }

    public decimal? ExpectedCash { get; init; }

    public decimal? CashVariance { get; init; }

    public string? BackupBeforePath { get; init; }

    public string? BackupAfterPath { get; init; }
}

public sealed class PitstopArchivedSaleRow
{
    public long SaleId { get; init; }

    public DateTimeOffset SoldAt { get; init; }

    public decimal Total { get; init; }

    public string PaymentMethod { get; init; } = string.Empty;

    public string? StaffDisplayName { get; init; }

    public decimal? BaseProductTotal { get; init; }

    public decimal? CardSurchargeAmount { get; init; }

    public string? SquareExternalRef { get; init; }
}

public sealed class PitstopEodArchiveRequest
{
    public string? OperatorName { get; init; }

    public long? OperatorStaffId { get; init; }

    public string? EventName { get; init; }

    public DateTimeOffset PeriodStartLocal { get; init; }

    public DateTimeOffset PeriodEndLocal { get; init; }

    public decimal TotalSales { get; init; }

    public decimal CashTotal { get; init; }

    public decimal CardChargedTotal { get; init; }

    public decimal CardBaseProductTotal { get; init; }

    public decimal CardSurchargeTotal { get; init; }

    public decimal EstimatedSquareFees { get; init; }

    public decimal NetTotal { get; init; }

    public string? PdfPath { get; init; }

    public PitstopReportData? ReportData { get; init; }

    public IReadOnlyList<string>? ReconciliationWarnings { get; init; }

    public string? Notes { get; init; }

    public decimal StartingFloat { get; init; }

    public decimal? CashCounted { get; init; }

    public decimal? FloatRemoved { get; init; }

    public decimal? ExpectedCash { get; init; }

    public decimal? CashVariance { get; init; }

    public string? BackupBeforePath { get; init; }

    public string? BackupAfterPath { get; init; }
}

public sealed class PitstopEodArchiveResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public long? BatchId { get; init; }

    public int SalesArchived { get; init; }

    public static PitstopEodArchiveResult Success(long batchId, int salesArchived) =>
        new() { Ok = true, BatchId = batchId, SalesArchived = salesArchived };

    public static PitstopEodArchiveResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

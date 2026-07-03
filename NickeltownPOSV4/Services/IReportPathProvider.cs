namespace NickeltownPOSV4.Services;



/// <summary>

/// Central resolver for where exported reports live on disk. All reports save under

/// <c>Documents\NickeltownPOS\Reports</c> so they survive MSIX app updates.

/// </summary>

public interface IReportPathProvider

{

    /// <summary>Root folder: <c>Documents\NickeltownPOS\Reports</c>. Created if missing.</summary>

    string GetRoot();



    /// <summary>Monthly bar tabs PDFs and the monthly activity CSV land here.</summary>

    string GetBarTallyReportsDirectory();



    /// <summary>Stock snapshot PDFs (and any future stock CSVs) land here.</summary>

    string GetStockReportsDirectory();



    /// <summary>Pitstop end-of-shift / cash-up reports will land here.</summary>

    string GetPitstopReportsDirectory();



    /// <summary>Per-tab history PDF exports from the tabs workspace.</summary>

    string GetTabHistoryExportsDirectory();

}


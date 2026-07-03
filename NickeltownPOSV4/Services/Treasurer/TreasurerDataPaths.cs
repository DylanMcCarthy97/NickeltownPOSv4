using System;
using System.IO;

namespace NickeltownPOSV4.Services.Treasurer;

public static class TreasurerDataPaths
{
    public static string DataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NickeltownPOSV4",
                "Data");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string ConfigDirectory
    {
        get
        {
            var dir = Path.Combine(DataDirectory, "Config");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string BackupDirectory
    {
        get
        {
            var dir = Path.Combine(DataDirectory, "Backups");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string GetDataFilePath(string fileName) => Path.Combine(DataDirectory, fileName);

    public static string GetConfigFilePath(string fileName) => Path.Combine(ConfigDirectory, fileName);

    // /// <summary>One-time import of treasurer JSON from legacy POSBar Data folder when V4 files are missing.</summary>
    // public static void TryImportFromLegacyPosBarData()
    // {
    //     var legacy = Path.Combine(
    //         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    //         "POSBar Data");
    //     if (!Directory.Exists(legacy))
    //     {
    //         return;
    //     }
    //
    //     string[] files =
    //     [
    //         "treasurer_accounts.json",
    //         "treasurer_transactions.json",
    //         "treasurer_categories.json",
    //         "treasurer_audit.json",
    //         "treasurer_bank_import_rules.json",
    //         "treasurer_bank_batches.json",
    //         "treasurer_bank_transactions.json",
    //         "treasurer_reconciliation_audit.json",
    //     ];
    //
    //     foreach (var f in files)
    //     {
    //         var target = GetDataFilePath(f);
    //         if (File.Exists(target))
    //         {
    //             continue;
    //         }
    //
    //         var source = Path.Combine(legacy, f);
    //         if (File.Exists(source))
    //         {
    //             File.Copy(source, target);
    //         }
    //     }
    // }
}

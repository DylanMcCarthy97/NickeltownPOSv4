namespace NickeltownPOSV4.Models.Migration;

/// <summary>Reusable migration wizard phases (UI can bind one step per value).</summary>
public enum MigrationWizardStep
{
    SelectV2Folder = 1,
    DetectJsonFiles = 2,
    PreviewCounts = 3,
    ValidateData = 4,
    BackupOriginals = 5,
    ImportToSqlite = 6,
    Summary = 7,
}

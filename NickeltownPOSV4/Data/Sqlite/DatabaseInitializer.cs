using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _factory;

    public DatabaseInitializer(SqliteConnectionFactory factory) => _factory = factory;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var conn = _factory.OpenConnection();
        foreach (var stmt in SchemaStatements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            conn.Execute(stmt);
        }

        ApplyOptionalColumnPatches(conn);
        RunCatalogV2Migration(conn);
        SeedMembershipFoundation(conn);

        return Task.CompletedTask;
    }

    private const string CatalogV2MigrationSettingsKey = "catalog_v2_migration_20260513";

    /// <summary>One-time: infer fixed Bar/Pitstop/Shared + sub-category from legacy Categories + visibility; clears CategoryId.</summary>
    private static void RunCatalogV2Migration(SqliteConnection conn)
    {
        var ran = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM Settings WHERE Key = @k",
            new { k = CatalogV2MigrationSettingsKey });
        if (ran > 0)
        {
            return;
        }

        var rows = conn.Query<(long Id, string? CatName, int ShowBar, int ShowPit, int OrderIn, int Track, int NotGonna)>(
            """
            SELECT
              i.Id,
              c.Name,
              COALESCE(i.ShowInBar, 1),
              COALESCE(i.ShowInPitstop, 0),
              COALESCE(i.OrderInMerchandise, 0),
              COALESCE(i.TrackStock, 1),
              COALESCE(i.NotGonnaOrderBack, 0)
            FROM Items i
            LEFT JOIN Categories c ON c.Id = i.CategoryId
            """);

        foreach (var r in rows)
        {
            StockCatalogTaxonomy.InferFromLegacy(
                r.CatName,
                r.ShowBar != 0,
                r.ShowPit != 0,
                out var bucket,
                out var sub);
            var bucketNorm = StockCatalogTaxonomy.NormalizeBucket(bucket);
            var subNorm = StockCatalogTaxonomy.NormalizeSubCategory(bucketNorm, sub);
            var mode = StockCatalogTaxonomy.StockModeFromLegacyFlags(r.Track, r.OrderIn, r.NotGonna);
            StockCatalogTaxonomy.ApplyStockModeToFlags(
                mode,
                out var tr,
                out var oi,
                out var ngb,
                out var inc,
                out var run);
            var isShared = bucketNorm == StockCatalogTaxonomy.BucketShared ? 1 : 0;
            conn.Execute(
                """
                UPDATE Items
                SET CatalogBucket = @b,
                    CatalogSubCategory = @s,
                    StockMode = @m,
                    TrackStock = @tr,
                    OrderInMerchandise = @oi,
                    NotGonnaOrderBack = @ngb,
                    IncludeInWeeklyStockReport = @inc,
                    IsRunOutItem = @run,
                    IsSharedItem = @ish,
                    CategoryId = NULL,
                    UpdatedAt = datetime('now')
                WHERE Id = @id
                """,
                new
                {
                    b = bucketNorm,
                    s = subNorm,
                    m = mode,
                    tr,
                    oi,
                    ngb,
                    inc,
                    run,
                    ish = isShared,
                    id = r.Id,
                });
        }

        conn.Execute(
            """
            INSERT INTO Settings (Key, Value, IsSecret, UpdatedAt)
            VALUES (@k, '1', 0, datetime('now'))
            """,
            new { k = CatalogV2MigrationSettingsKey });
    }

    private const string StockVolunteerMigrationSettingsKey = "stock_volunteer_migration_20260603";

    /// <summary>
    /// One-time backfill: par level JSON → PreferredStockLevel, LowStockThreshold → WarnMeBelow,
    /// weekly report flag → ShowOnShoppingList.
    /// </summary>
    private static void RunStockVolunteerFieldMigration(SqliteConnection conn)
    {
        var ran = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM Settings WHERE Key = @k",
            new { k = StockVolunteerMigrationSettingsKey });
        if (ran > 0)
        {
            return;
        }

        conn.Execute(
            """
            UPDATE Items
            SET WarnMeBelow = LowStockThreshold
            WHERE WarnMeBelow IS NULL AND LowStockThreshold IS NOT NULL AND LowStockThreshold > 0
            """);

        conn.Execute(
            """
            UPDATE Items
            SET ShowOnShoppingList = COALESCE(IncludeInWeeklyStockReport, 1)
            WHERE ShowOnShoppingList IS NULL
            """);

        var rows = conn.Query<(long Id, string? ItemDescription)>(
            "SELECT Id, ItemDescription FROM Items WHERE PreferredStockLevel IS NULL AND ItemDescription IS NOT NULL");

        foreach (var (id, desc) in rows)
        {
            var meta = Services.Stock.StockItemMetadataSerializer.Parse(desc, isShotMixer: false);
            if (meta.ParLevel is > 0)
            {
                conn.Execute(
                    "UPDATE Items SET PreferredStockLevel = @p WHERE Id = @id",
                    new { p = meta.ParLevel.Value, id });
            }
        }

        conn.Execute(
            """
            INSERT INTO Settings (Key, Value, IsSecret, UpdatedAt)
            VALUES (@k, '1', 0, datetime('now'))
            """,
            new { k = StockVolunteerMigrationSettingsKey });
    }

    /// <summary>Idempotent ALTERs for installs that created the DB before newer columns existed.</summary>
    private static void ApplyOptionalColumnPatches(SqliteConnection conn)
    {
        TryAddColumn(conn, "Items", "TrackStock", "ALTER TABLE Items ADD COLUMN TrackStock INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(conn, "Items", "ImagePath", "ALTER TABLE Items ADD COLUMN ImagePath TEXT");
        TryAddColumn(conn, "Items", "CostPrice", "ALTER TABLE Items ADD COLUMN CostPrice REAL");
        TryAddColumn(conn, "Items", "LowStockThreshold", "ALTER TABLE Items ADD COLUMN LowStockThreshold INTEGER");
        TryAddColumn(conn, "Items", "UsesOpenPrice", "ALTER TABLE Items ADD COLUMN UsesOpenPrice INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "ItemPrices", "PriceKind", "ALTER TABLE ItemPrices ADD COLUMN PriceKind TEXT NOT NULL DEFAULT 'Bar'");
        TryAddColumn(conn, "Bartenders", "PinSalt", "ALTER TABLE Bartenders ADD COLUMN PinSalt TEXT");
        TryAddColumn(conn, "Bartenders", "UiTheme", "ALTER TABLE Bartenders ADD COLUMN UiTheme TEXT");
        TryAddColumn(conn, "Bartenders", "LegacyPinPlain", "ALTER TABLE Bartenders ADD COLUMN LegacyPinPlain TEXT");
        TryAddColumn(conn, "Bartenders", "IsDeveloper", "ALTER TABLE Bartenders ADD COLUMN IsDeveloper INTEGER NOT NULL DEFAULT 0");
        conn.Execute("CREATE INDEX IF NOT EXISTS IX_Bartenders_Active_LegacyPinPlain ON Bartenders(IsActive, LegacyPinPlain) WHERE LegacyPinPlain IS NOT NULL");
        conn.Execute("CREATE INDEX IF NOT EXISTS IX_TabEntries_TabId_EntryType ON TabEntries(TabId, EntryType)");
        BackfillBartenderLegacyPinPlain(conn);
        TryAddColumn(conn, "Tabs", "IsGuest", "ALTER TABLE Tabs ADD COLUMN IsGuest INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "Tabs", "Notes", "ALTER TABLE Tabs ADD COLUMN Notes TEXT");
        TryAddColumn(conn, "Tabs", "IsDeleted", "ALTER TABLE Tabs ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "Tabs", "LastActivityAt", "ALTER TABLE Tabs ADD COLUMN LastActivityAt TEXT");
        TryAddColumn(conn, "Tabs", "TabType", "ALTER TABLE Tabs ADD COLUMN TabType TEXT");
        TryAddColumn(conn, "Tabs", "IsClosed", "ALTER TABLE Tabs ADD COLUMN IsClosed INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "Tabs", "ClosedAt", "ALTER TABLE Tabs ADD COLUMN ClosedAt TEXT");
        TryAddColumn(conn, "Tabs", "ClosedByBartenderId", "ALTER TABLE Tabs ADD COLUMN ClosedByBartenderId INTEGER");
        TryAddColumn(conn, "Tabs", "CloseReason", "ALTER TABLE Tabs ADD COLUMN CloseReason TEXT");
        TryAddColumn(conn, "Items", "ShowInPitstop", "ALTER TABLE Items ADD COLUMN ShowInPitstop INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "Items", "ShowInBar", "ALTER TABLE Items ADD COLUMN ShowInBar INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(conn, "Items", "OrderInMerchandise", "ALTER TABLE Items ADD COLUMN OrderInMerchandise INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "PitstopSales", "SaleGuid", "ALTER TABLE PitstopSales ADD COLUMN SaleGuid TEXT");
        TryAddColumn(conn, "PitstopSales", "PaymentMethod", "ALTER TABLE PitstopSales ADD COLUMN PaymentMethod TEXT");
        TryAddColumn(conn, "PitstopSales", "SaleMode", "ALTER TABLE PitstopSales ADD COLUMN SaleMode TEXT");
        TryAddColumn(conn, "PitstopSales", "BartenderId", "ALTER TABLE PitstopSales ADD COLUMN BartenderId INTEGER");
        TryAddColumn(conn, "PitstopSales", "StaffDisplayName", "ALTER TABLE PitstopSales ADD COLUMN StaffDisplayName TEXT");
        TryAddColumn(conn, "PitstopSales", "SquareExternalRef", "ALTER TABLE PitstopSales ADD COLUMN SquareExternalRef TEXT");
        TryAddColumn(conn, "PitstopSales", "BaseProductTotal", "ALTER TABLE PitstopSales ADD COLUMN BaseProductTotal REAL");
        TryAddColumn(conn, "PitstopSales", "CardSurchargePercent", "ALTER TABLE PitstopSales ADD COLUMN CardSurchargePercent REAL");
        TryAddColumn(conn, "PitstopSales", "CardSurchargeAmount", "ALTER TABLE PitstopSales ADD COLUMN CardSurchargeAmount REAL");
        TryAddColumn(conn, "Items", "CatalogBucket", "ALTER TABLE Items ADD COLUMN CatalogBucket TEXT NOT NULL DEFAULT 'Bar'");
        TryAddColumn(conn, "Items", "CatalogSubCategory", "ALTER TABLE Items ADD COLUMN CatalogSubCategory TEXT NOT NULL DEFAULT 'Drinks'");
        TryAddColumn(conn, "Items", "StockMode", "ALTER TABLE Items ADD COLUMN StockMode TEXT NOT NULL DEFAULT 'Tracked'");
        TryAddColumn(conn, "Items", "NotGonnaOrderBack", "ALTER TABLE Items ADD COLUMN NotGonnaOrderBack INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "Items", "IncludeInWeeklyStockReport", "ALTER TABLE Items ADD COLUMN IncludeInWeeklyStockReport INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(conn, "Items", "IsRunOutItem", "ALTER TABLE Items ADD COLUMN IsRunOutItem INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "Items", "IsSharedItem", "ALTER TABLE Items ADD COLUMN IsSharedItem INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "Items", "IsOnSpecial", "ALTER TABLE Items ADD COLUMN IsOnSpecial INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "Items", "SpecialType", "ALTER TABLE Items ADD COLUMN SpecialType TEXT");
        TryAddColumn(conn, "Items", "SpecialValue", "ALTER TABLE Items ADD COLUMN SpecialValue TEXT");
        TryAddColumn(conn, "Items", "SpecialLabel", "ALTER TABLE Items ADD COLUMN SpecialLabel TEXT");
        TryAddColumn(conn, "Items", "SpecialAppliesTo", "ALTER TABLE Items ADD COLUMN SpecialAppliesTo TEXT");
        TryAddColumn(conn, "Items", "AlternateSkusJson", "ALTER TABLE Items ADD COLUMN AlternateSkusJson TEXT");
        TryAddColumn(conn, "Items", "ItemDescription", "ALTER TABLE Items ADD COLUMN ItemDescription TEXT");
        TryAddColumn(conn, "PitstopSaleItems", "ItemId", "ALTER TABLE PitstopSaleItems ADD COLUMN ItemId INTEGER REFERENCES Items(Id)");
        TryAddColumn(conn, "PitstopSaleItems", "UnitPrice", "ALTER TABLE PitstopSaleItems ADD COLUMN UnitPrice REAL");
        TryAddColumn(conn, "TabEntries", "CommitBatchId", "ALTER TABLE TabEntries ADD COLUMN CommitBatchId TEXT");
        TryAddColumn(conn, "MoneyMovements", "CommitBatchId", "ALTER TABLE MoneyMovements ADD COLUMN CommitBatchId TEXT");
        TryAddColumn(conn, "Payments", "CommitBatchId", "ALTER TABLE Payments ADD COLUMN CommitBatchId TEXT");
        TryAddColumn(conn, "Payments", "SquarePaymentId", "ALTER TABLE Payments ADD COLUMN SquarePaymentId TEXT");
        TryAddColumn(conn, "Payments", "SquareCheckoutId", "ALTER TABLE Payments ADD COLUMN SquareCheckoutId TEXT");
        TryAddColumn(conn, "Payments", "BaseAmount", "ALTER TABLE Payments ADD COLUMN BaseAmount REAL");
        TryAddColumn(conn, "Payments", "SurchargeAmount", "ALTER TABLE Payments ADD COLUMN SurchargeAmount REAL");
        TryAddColumn(conn, "Payments", "ChargedAmount", "ALTER TABLE Payments ADD COLUMN ChargedAmount REAL");
        TryAddColumn(conn, "PitstopSales", "IdempotencyKey", "ALTER TABLE PitstopSales ADD COLUMN IdempotencyKey TEXT");
        TryAddColumn(conn, "PitstopSales", "CashReceived", "ALTER TABLE PitstopSales ADD COLUMN CashReceived REAL");
        TryAddColumn(conn, "PitstopSales", "CashChange", "ALTER TABLE PitstopSales ADD COLUMN CashChange REAL");
        TryAddColumn(conn, "PitstopSales", "SquareCheckoutId", "ALTER TABLE PitstopSales ADD COLUMN SquareCheckoutId TEXT");
        TryAddColumn(conn, "PitstopSales", "PitstopEodBatchId", "ALTER TABLE PitstopSales ADD COLUMN PitstopEodBatchId INTEGER REFERENCES PitstopEodBatches(Id)");
        TryAddColumn(conn, "PitstopSales", "Status", "ALTER TABLE PitstopSales ADD COLUMN Status TEXT NOT NULL DEFAULT 'Active'");
        TryAddColumn(conn, "PitstopSales", "VoidedAt", "ALTER TABLE PitstopSales ADD COLUMN VoidedAt TEXT");
        TryAddColumn(conn, "PitstopSales", "VoidedByStaffId", "ALTER TABLE PitstopSales ADD COLUMN VoidedByStaffId INTEGER");
        TryAddColumn(conn, "PitstopSales", "VoidedByStaffName", "ALTER TABLE PitstopSales ADD COLUMN VoidedByStaffName TEXT");
        TryAddColumn(conn, "PitstopSales", "VoidReason", "ALTER TABLE PitstopSales ADD COLUMN VoidReason TEXT");
        TryAddColumn(conn, "PitstopSales", "StockWasDeducted", "ALTER TABLE PitstopSales ADD COLUMN StockWasDeducted INTEGER NOT NULL DEFAULT 1");

        // Stock management volunteer fields (Preferred Level, shopping list, pack size, count date).
        TryAddColumn(conn, "Items", "PreferredStockLevel", "ALTER TABLE Items ADD COLUMN PreferredStockLevel INTEGER");
        TryAddColumn(conn, "Items", "WarnMeBelow", "ALTER TABLE Items ADD COLUMN WarnMeBelow INTEGER");
        TryAddColumn(conn, "Items", "PurchaseUnitQty", "ALTER TABLE Items ADD COLUMN PurchaseUnitQty INTEGER");
        TryAddColumn(conn, "Items", "ShowOnShoppingList", "ALTER TABLE Items ADD COLUMN ShowOnShoppingList INTEGER");
        TryAddColumn(conn, "Items", "LastStockCountDate", "ALTER TABLE Items ADD COLUMN LastStockCountDate TEXT");
        RunStockVolunteerFieldMigration(conn);

        conn.Execute(
            """
            CREATE TABLE IF NOT EXISTS StockPurchases (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              ItemId INTEGER NOT NULL REFERENCES Items(Id) ON DELETE CASCADE,
              PurchaseDate TEXT NOT NULL,
              PacksBought INTEGER NOT NULL,
              ItemsPerPack INTEGER NOT NULL,
              TotalItems INTEGER NOT NULL,
              TotalPaid REAL NOT NULL,
              CostEach REAL NOT NULL,
              Notes TEXT,
              CreatedAt TEXT NOT NULL
            );
            """);

        TryAddColumn(conn, "SquarePaymentAttempts", "Status", "ALTER TABLE SquarePaymentAttempts ADD COLUMN Status TEXT NOT NULL DEFAULT 'Pending'");
        TryAddColumn(conn, "SquarePaymentAttempts", "RecoveryStatus", "ALTER TABLE SquarePaymentAttempts ADD COLUMN RecoveryStatus TEXT");
        TryAddColumn(conn, "SquarePaymentAttempts", "RecoveryNote", "ALTER TABLE SquarePaymentAttempts ADD COLUMN RecoveryNote TEXT");
        TryAddColumn(conn, "SquarePaymentAttempts", "InitiatedByStaffId", "ALTER TABLE SquarePaymentAttempts ADD COLUMN InitiatedByStaffId INTEGER");
        TryAddColumn(conn, "SquarePaymentAttempts", "InitiatedByStaffName", "ALTER TABLE SquarePaymentAttempts ADD COLUMN InitiatedByStaffName TEXT");
        TryAddColumn(conn, "SquarePaymentAttempts", "RecoveryPayloadJson", "ALTER TABLE SquarePaymentAttempts ADD COLUMN RecoveryPayloadJson TEXT");

        TryAddColumn(conn, "PitstopEodBatches", "Notes", "ALTER TABLE PitstopEodBatches ADD COLUMN Notes TEXT");
        TryAddColumn(conn, "PitstopEodBatches", "OperatorStaffId", "ALTER TABLE PitstopEodBatches ADD COLUMN OperatorStaffId INTEGER");
        TryAddColumn(conn, "PitstopEodBatches", "StartingFloat", "ALTER TABLE PitstopEodBatches ADD COLUMN StartingFloat REAL NOT NULL DEFAULT 0");
        TryAddColumn(conn, "PitstopEodBatches", "CashCounted", "ALTER TABLE PitstopEodBatches ADD COLUMN CashCounted REAL");
        TryAddColumn(conn, "PitstopEodBatches", "FloatRemoved", "ALTER TABLE PitstopEodBatches ADD COLUMN FloatRemoved REAL");
        TryAddColumn(conn, "PitstopEodBatches", "ExpectedCash", "ALTER TABLE PitstopEodBatches ADD COLUMN ExpectedCash REAL");
        TryAddColumn(conn, "PitstopEodBatches", "CashVariance", "ALTER TABLE PitstopEodBatches ADD COLUMN CashVariance REAL");
        TryAddColumn(conn, "PitstopEodBatches", "BackupBeforePath", "ALTER TABLE PitstopEodBatches ADD COLUMN BackupBeforePath TEXT");
        TryAddColumn(conn, "PitstopEodBatches", "BackupAfterPath", "ALTER TABLE PitstopEodBatches ADD COLUMN BackupAfterPath TEXT");

        TryAddColumn(conn, "MembershipApplications", "CreatedBy", "ALTER TABLE MembershipApplications ADD COLUMN CreatedBy TEXT");
        TryAddColumn(conn, "MembershipApplications", "PaperDeclarationSigned", "ALTER TABLE MembershipApplications ADD COLUMN PaperDeclarationSigned INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "SelectedFee", "ALTER TABLE MembershipApplications ADD COLUMN SelectedFee REAL");
        TryAddColumn(conn, "MembershipApplications", "FeeType", "ALTER TABLE MembershipApplications ADD COLUMN FeeType TEXT");
        TryAddColumn(conn, "MembershipApplications", "ReceiptIssued", "ALTER TABLE MembershipApplications ADD COLUMN ReceiptIssued INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "ReceiptDate", "ALTER TABLE MembershipApplications ADD COLUMN ReceiptDate TEXT");
        TryAddColumn(conn, "MembershipApplications", "MembershipAcceptedDate", "ALTER TABLE MembershipApplications ADD COLUMN MembershipAcceptedDate TEXT");
        TryAddColumn(conn, "MembershipApplications", "AddedToDistributionList", "ALTER TABLE MembershipApplications ADD COLUMN AddedToDistributionList INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "MembershipCardIssued", "ALTER TABLE MembershipApplications ADD COLUMN MembershipCardIssued INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "WelcomeBagIssued", "ALTER TABLE MembershipApplications ADD COLUMN WelcomeBagIssued INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "HasNoVehicle", "ALTER TABLE MembershipApplications ADD COLUMN HasNoVehicle INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "AddedToMemberRegister", "ALTER TABLE MembershipApplications ADD COLUMN AddedToMemberRegister INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "AddedToEmailDistributionList", "ALTER TABLE MembershipApplications ADD COLUMN AddedToEmailDistributionList INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "AddedToSmsDistributionList", "ALTER TABLE MembershipApplications ADD COLUMN AddedToSmsDistributionList INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "MembershipApplications", "PaymentStatus", "ALTER TABLE MembershipApplications ADD COLUMN PaymentStatus TEXT NOT NULL DEFAULT 'AwaitingPayment'");
        TryAddColumn(conn, "MembershipApplications", "PaymentMethod", "ALTER TABLE MembershipApplications ADD COLUMN PaymentMethod TEXT");
        TryAddColumn(conn, "MembershipApplications", "ReceiptNumber", "ALTER TABLE MembershipApplications ADD COLUMN ReceiptNumber TEXT");
        TryAddColumn(conn, "MembershipApplications", "PaymentEnteredBy", "ALTER TABLE MembershipApplications ADD COLUMN PaymentEnteredBy TEXT");
        TryAddColumn(conn, "MembershipApplications", "PaymentNotes", "ALTER TABLE MembershipApplications ADD COLUMN PaymentNotes TEXT");
        TryAddColumn(conn, "MembershipApplications", "ApprovedBy", "ALTER TABLE MembershipApplications ADD COLUMN ApprovedBy TEXT");
        TryAddColumn(conn, "MembershipApplications", "ApprovalDate", "ALTER TABLE MembershipApplications ADD COLUMN ApprovalDate TEXT");
        TryAddColumn(conn, "MembershipApplications", "MembershipStart", "ALTER TABLE MembershipApplications ADD COLUMN MembershipStart TEXT");
        TryAddColumn(conn, "MembershipApplications", "MembershipExpiry", "ALTER TABLE MembershipApplications ADD COLUMN MembershipExpiry TEXT");
        TryAddColumn(conn, "MembershipApplications", "MembershipNumber", "ALTER TABLE MembershipApplications ADD COLUMN MembershipNumber TEXT");

        conn.Execute(
            """
            UPDATE MembershipApplications
            SET AddedToMemberRegister = 1
            WHERE AddedToDistributionList = 1 AND AddedToMemberRegister = 0;
            """);

        conn.Execute(
            """
            CREATE TABLE IF NOT EXISTS MembershipApplicationNotes (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              ApplicationId INTEGER NOT NULL REFERENCES MembershipApplications(Id) ON DELETE CASCADE,
              Author TEXT NOT NULL,
              Text TEXT NOT NULL,
              CreatedAt TEXT NOT NULL
            );
            """);

        conn.Execute(
            """
            CREATE INDEX IF NOT EXISTS IX_MembershipApplicationNotes_ApplicationId
            ON MembershipApplicationNotes(ApplicationId, datetime(CreatedAt) DESC);
            """);

        conn.Execute(
            """
            CREATE TABLE IF NOT EXISTS MembershipApplicationTimelineEvents (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              ApplicationId INTEGER NOT NULL REFERENCES MembershipApplications(Id) ON DELETE CASCADE,
              EventType TEXT NOT NULL,
              UserName TEXT NOT NULL,
              Description TEXT NOT NULL,
              OccurredAt TEXT NOT NULL
            );
            """);

        conn.Execute(
            """
            CREATE INDEX IF NOT EXISTS IX_MembershipApplicationTimelineEvents_ApplicationId
            ON MembershipApplicationTimelineEvents(ApplicationId, datetime(OccurredAt) DESC);
            """);

        conn.Execute(
            """
            CREATE TABLE IF NOT EXISTS AuditLog (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              OccurredAt TEXT NOT NULL,
              StaffId INTEGER,
              StaffName TEXT,
              StaffRole TEXT,
              ActionType TEXT NOT NULL,
              EntityType TEXT,
              EntityId TEXT,
              Amount REAL,
              Reason TEXT,
              Success INTEGER NOT NULL DEFAULT 1,
              DetailsJson TEXT,
              CreatedAt TEXT NOT NULL
            );
            """);

        conn.Execute(
            """
            CREATE INDEX IF NOT EXISTS IX_AuditLog_OccurredAt
            ON AuditLog(OccurredAt DESC);
            """);
        conn.Execute(
            """
            CREATE INDEX IF NOT EXISTS IX_AuditLog_Action
            ON AuditLog(ActionType);
            """);
        conn.Execute(
            """
            CREATE INDEX IF NOT EXISTS IX_AuditLog_Entity
            ON AuditLog(EntityType, EntityId);
            """);

        conn.Execute(
            """
            UPDATE PitstopSales
            SET Status = 'Archived'
            WHERE PitstopEodBatchId IS NOT NULL
              AND (Status IS NULL OR trim(Status) = '' OR Status = 'Active');
            """);
        conn.Execute(
            """
            UPDATE PitstopSales
            SET Status = 'Active'
            WHERE Status IS NULL OR trim(Status) = '';
            """);

        conn.Execute(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_SquarePaymentAttempts_IdempotencyKey
            ON SquarePaymentAttempts(IdempotencyKey);
            """);
        conn.Execute(
            """
            CREATE INDEX IF NOT EXISTS IX_SquarePaymentAttempts_SquarePaymentId
            ON SquarePaymentAttempts(SquarePaymentId);
            """);
        conn.Execute(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_PitstopSales_IdempotencyKey
            ON PitstopSales(IdempotencyKey)
            WHERE IdempotencyKey IS NOT NULL AND trim(IdempotencyKey) != '';
            """);
        conn.Execute(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Payments_ExternalRef
            ON Payments(ExternalRef)
            WHERE ExternalRef IS NOT NULL AND trim(ExternalRef) != '';
            """);

        conn.Execute(
            """
            UPDATE Tabs
            SET TabType = 'Guest'
            WHERE (TabType IS NULL OR trim(TabType) = '')
              AND COALESCE(IsGuest,0) != 0
            """);
        conn.Execute(
            """
            UPDATE Tabs
            SET TabType = 'Member'
            WHERE (TabType IS NULL OR trim(TabType) = '')
              AND COALESCE(IsGuest,0) = 0
            """);

        conn.Execute(
            """
            UPDATE Tabs
            SET IsArchived = 1
            WHERE COALESCE(IsClosed,0) != 0
              AND COALESCE(IsArchived,0) = 0
            """);
    }

    private static void BackfillBartenderLegacyPinPlain(SqliteConnection conn)
    {
        var rows = conn.Query<(long Id, string? RawJson)>(
            """
            SELECT Id, RawJson
            FROM Bartenders
            WHERE LegacyPinPlain IS NULL OR TRIM(LegacyPinPlain) = ''
            """);

        foreach (var (id, rawJson) in rows)
        {
            var plain = StaffPinPinLookupHelper.TryExtractLegacyPlainPin(rawJson);
            if (plain is null)
            {
                continue;
            }

            conn.Execute(
                "UPDATE Bartenders SET LegacyPinPlain = @Plain WHERE Id = @Id",
                new { Plain = plain, Id = id });
        }
    }

    private static void TryAddColumn(SqliteConnection conn, string table, string column, string alterSql)
    {
        var n = conn.ExecuteScalar<long>(
            $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name=@col",
            new { col = column });
        if (n == 0)
        {
            conn.Execute(alterSql);
        }
    }

    private static readonly string[] SchemaStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS Categories (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          LegacyId TEXT,
          LegacyKey TEXT,
          Name TEXT NOT NULL,
          SortOrder INTEGER NOT NULL DEFAULT 0,
          IsActive INTEGER NOT NULL DEFAULT 1,
          CreatedAt TEXT NOT NULL,
          UpdatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Items (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          LegacyId TEXT,
          LegacyKey TEXT,
          Name TEXT NOT NULL,
          Sku TEXT,
          CategoryId INTEGER REFERENCES Categories(Id),
          ItemType TEXT NOT NULL DEFAULT 'Item',
          StockQty INTEGER NOT NULL DEFAULT 0,
          RawJson TEXT,
          IsActive INTEGER NOT NULL DEFAULT 1,
          CreatedAt TEXT NOT NULL,
          UpdatedAt TEXT NOT NULL,
          UNIQUE(LegacyId)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS ItemPrices (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          ItemId INTEGER NOT NULL REFERENCES Items(Id) ON DELETE CASCADE,
          Price REAL NOT NULL,
          EffectiveFrom TEXT NOT NULL,
          EffectiveTo TEXT,
          CreatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS StockMovements (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          ItemId INTEGER NOT NULL REFERENCES Items(Id) ON DELETE CASCADE,
          DeltaQty INTEGER NOT NULL,
          Reason TEXT,
          Reference TEXT,
          CreatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Bartenders (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          LegacyId TEXT,
          LegacyKey TEXT,
          Name TEXT NOT NULL,
          PinHash TEXT,
          PinSalt TEXT,
          Role TEXT,
          IsActive INTEGER NOT NULL DEFAULT 1,
          RawJson TEXT,
          CreatedAt TEXT NOT NULL,
          UpdatedAt TEXT NOT NULL,
          UNIQUE(LegacyId)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Members (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          LegacyId TEXT,
          LegacyKey TEXT,
          Name TEXT,
          Email TEXT,
          Phone TEXT,
          Balance REAL NOT NULL DEFAULT 0,
          RawJson TEXT,
          IsActive INTEGER NOT NULL DEFAULT 1,
          CreatedAt TEXT NOT NULL,
          UpdatedAt TEXT NOT NULL,
          UNIQUE(LegacyId)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Tabs (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          LegacyId TEXT,
          LegacyKey TEXT,
          Name TEXT NOT NULL,
          DisplayName TEXT,
          Balance REAL NOT NULL DEFAULT 0,
          MemberId TEXT,
          IsMember INTEGER NOT NULL DEFAULT 1,
          IsGuest INTEGER NOT NULL DEFAULT 0,
          TabType TEXT NOT NULL DEFAULT 'Member',
          IsArchived INTEGER NOT NULL DEFAULT 0,
          IsDeleted INTEGER NOT NULL DEFAULT 0,
          IsClosed INTEGER NOT NULL DEFAULT 0,
          ClosedAt TEXT,
          ClosedByBartenderId INTEGER,
          CloseReason TEXT,
          Notes TEXT,
          LastDrinkSummary TEXT,
          LastActivityAt TEXT,
          RawJson TEXT,
          CreatedAt TEXT NOT NULL,
          UpdatedAt TEXT NOT NULL,
          UNIQUE(LegacyId)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS TabEntries (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          TabId INTEGER NOT NULL REFERENCES Tabs(Id) ON DELETE CASCADE,
          LegacyEntryId TEXT,
          EntryType TEXT,
          Amount REAL,
          Note TEXT,
          OccurredAt TEXT,
          RawJson TEXT,
          CreatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS MoneyMovements (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          TabId INTEGER REFERENCES Tabs(Id) ON DELETE SET NULL,
          MemberId INTEGER REFERENCES Members(Id) ON DELETE SET NULL,
          Amount REAL NOT NULL,
          MovementType TEXT NOT NULL,
          Note TEXT,
          OccurredAt TEXT,
          RawJson TEXT,
          CreatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS PitstopSales (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          LegacyId TEXT,
          SoldAt TEXT,
          Total REAL,
          RawJson TEXT,
          CreatedAt TEXT NOT NULL,
          UNIQUE(LegacyId)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS PitstopSaleItems (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          PitstopSaleId INTEGER NOT NULL REFERENCES PitstopSales(Id) ON DELETE CASCADE,
          LegacyLineId TEXT,
          Sku TEXT,
          ItemName TEXT,
          Quantity INTEGER NOT NULL DEFAULT 0,
          LineTotal REAL,
          RawJson TEXT,
          CreatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS PitstopEodBatches (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          ArchivedAt TEXT NOT NULL,
          OperatorName TEXT,
          EventName TEXT,
          PeriodStartLocal TEXT,
          PeriodEndLocal TEXT,
          TotalSales REAL NOT NULL DEFAULT 0,
          CashTotal REAL NOT NULL DEFAULT 0,
          CardChargedTotal REAL NOT NULL DEFAULT 0,
          CardBaseProductTotal REAL NOT NULL DEFAULT 0,
          CardSurchargeTotal REAL NOT NULL DEFAULT 0,
          EstimatedSquareFees REAL NOT NULL DEFAULT 0,
          NetTotal REAL NOT NULL DEFAULT 0,
          SaleCount INTEGER NOT NULL DEFAULT 0,
          PdfPath TEXT,
          ReportDataJson TEXT,
          ReconciliationWarningsJson TEXT,
          CreatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS PitstopHeldSales (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          HeldAt TEXT NOT NULL,
          StaffId INTEGER,
          StaffDisplayName TEXT,
          LineCount INTEGER NOT NULL DEFAULT 0,
          TotalAmount REAL NOT NULL DEFAULT 0,
          CreatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS PitstopHeldSaleLines (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          HeldSaleId INTEGER NOT NULL REFERENCES PitstopHeldSales(Id) ON DELETE CASCADE,
          ItemId INTEGER NOT NULL,
          ItemName TEXT NOT NULL,
          Sku TEXT,
          CategoryName TEXT,
          SubCategory TEXT,
          UnitPrice REAL NOT NULL,
          Quantity INTEGER NOT NULL,
          SortOrder INTEGER NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Payments (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          TabId INTEGER REFERENCES Tabs(Id) ON DELETE SET NULL,
          Amount REAL NOT NULL,
          Method TEXT,
          ExternalRef TEXT,
          CreatedAt TEXT NOT NULL,
          RawJson TEXT
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Settings (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          Key TEXT NOT NULL UNIQUE,
          Value TEXT,
          IsSecret INTEGER NOT NULL DEFAULT 0,
          UpdatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS MigrationRuns (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          RunId TEXT NOT NULL UNIQUE,
          SourceRoot TEXT,
          StartedAtUtc TEXT NOT NULL,
          CompletedAtUtc TEXT,
          Notes TEXT
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS MigrationImportedFiles (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          FileKind INTEGER NOT NULL,
          SourcePath TEXT NOT NULL,
          ContentSha256 TEXT NOT NULL,
          ImportedAtUtc TEXT NOT NULL,
          UNIQUE(FileKind, SourcePath, ContentSha256)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS TabBarFavoriteItems (
          TabLegacyId TEXT NOT NULL,
          ItemId INTEGER NOT NULL REFERENCES Items(Id) ON DELETE CASCADE,
          SortOrder INTEGER NOT NULL DEFAULT 0,
          CreatedAt TEXT NOT NULL,
          PRIMARY KEY (TabLegacyId, ItemId)
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS IX_TabBarFavoriteItems_TabOrder
        ON TabBarFavoriteItems(TabLegacyId, SortOrder);
        """,
        """
        CREATE TABLE IF NOT EXISTS SquarePaymentAttempts (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          IdempotencyKey TEXT NOT NULL,
          Status TEXT NOT NULL,
          PaymentType TEXT NOT NULL,
          TabId INTEGER REFERENCES Tabs(Id) ON DELETE SET NULL,
          TabLegacyId TEXT,
          PitstopSaleId INTEGER REFERENCES PitstopSales(Id) ON DELETE SET NULL,
          PitstopSaleGuid TEXT,
          SquareCheckoutId TEXT,
          SquarePaymentId TEXT,
          BaseAmount REAL NOT NULL,
          SurchargeAmount REAL NOT NULL DEFAULT 0,
          ChargedAmount REAL NOT NULL,
          FailureReason TEXT,
          LocalPaymentId INTEGER REFERENCES Payments(Id) ON DELETE SET NULL,
          CreatedAt TEXT NOT NULL,
          UpdatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS AuditLog (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          OccurredAt TEXT NOT NULL,
          StaffId INTEGER,
          StaffName TEXT,
          StaffRole TEXT,
          ActionType TEXT NOT NULL,
          EntityType TEXT,
          EntityId TEXT,
          Amount REAL,
          Reason TEXT,
          Success INTEGER NOT NULL DEFAULT 1,
          DetailsJson TEXT,
          CreatedAt TEXT NOT NULL
        );
        """,
        """
        INSERT INTO Categories (LegacyKey, Name, SortOrder, IsActive, CreatedAt, UpdatedAt)
        SELECT 'default', 'General', 0, 1, datetime('now'), datetime('now')
        WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE LegacyKey = 'default');
        """,
        """
        CREATE TABLE IF NOT EXISTS MembershipSettings (
          Id INTEGER PRIMARY KEY CHECK (Id = 1),
          MembershipYearLabel TEXT NOT NULL,
          MembershipYearStart TEXT NOT NULL,
          MembershipYearEnd TEXT NOT NULL,
          JoiningFeeFull REAL NOT NULL DEFAULT 65.00,
          JoiningFeeHalf REAL NOT NULL DEFAULT 32.50,
          RenewalFee REAL NOT NULL DEFAULT 0,
          ReminderDaysBeforeExpiry INTEGER NOT NULL DEFAULT 30,
          CommitteeEmail TEXT,
          ClubName TEXT,
          ClubAbn TEXT,
          ClubPoBox TEXT,
          ClubPhone TEXT,
          ClubEmail TEXT,
          LogoPath TEXT,
          UpdatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS MembershipFormContent (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          SectionKey TEXT NOT NULL UNIQUE,
          Title TEXT,
          Body TEXT NOT NULL,
          SortOrder INTEGER NOT NULL DEFAULT 0,
          UpdatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS MembershipApplications (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          ApplicationNumber TEXT,
          Source TEXT NOT NULL,
          Status TEXT NOT NULL,
          Surname TEXT,
          GivenNames TEXT,
          ChildrenUnder18 TEXT,
          Address TEXT,
          PostCode TEXT,
          DateOfBirth TEXT,
          Email TEXT,
          Phone TEXT,
          Mobile TEXT,
          AdditionalComments TEXT,
          SignatureData TEXT,
          SignedAt TEXT,
          SubmittedAt TEXT NOT NULL,
          ReviewedAt TEXT,
          ApprovedAt TEXT,
          RejectedAt TEXT,
          RejectionReason TEXT,
          CreatedAt TEXT NOT NULL,
          UpdatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS MembershipApplicationVehicles (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          ApplicationId INTEGER NOT NULL REFERENCES MembershipApplications(Id) ON DELETE CASCADE,
          MakeModel TEXT,
          Year TEXT,
          BodyType TEXT,
          Engine TEXT,
          RegistrationNumber TEXT,
          ClubRego TEXT,
          Colour TEXT,
          Modifications TEXT,
          SortOrder INTEGER NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS MembershipMembers (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          ApplicationId INTEGER REFERENCES MembershipApplications(Id) ON DELETE SET NULL,
          PosMemberId INTEGER REFERENCES Members(Id) ON DELETE SET NULL,
          MemberNumber TEXT,
          Surname TEXT,
          GivenNames TEXT,
          Email TEXT,
          Phone TEXT,
          Mobile TEXT,
          Address TEXT,
          PostCode TEXT,
          DateOfBirth TEXT,
          MembershipYearLabel TEXT,
          MembershipStartsAt TEXT,
          MembershipExpiresAt TEXT,
          IsActive INTEGER NOT NULL DEFAULT 1,
          ReceiptIssuedAt TEXT,
          AddedToDistributionList INTEGER NOT NULL DEFAULT 0,
          CardIssued INTEGER NOT NULL DEFAULT 0,
          WelcomeBagIssued INTEGER NOT NULL DEFAULT 0,
          CreatedAt TEXT NOT NULL,
          UpdatedAt TEXT NOT NULL
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS IX_MembershipApplications_Status
        ON MembershipApplications(Status);
        """,
        """
        CREATE INDEX IF NOT EXISTS IX_MembershipMembers_Active
        ON MembershipMembers(IsActive);
        """,
    ];

    private static void SeedMembershipFoundation(SqliteConnection conn)
    {
        var settingsCount = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM MembershipSettings WHERE Id = 1");
        if (settingsCount == 0)
        {
            conn.Execute(
                """
                INSERT INTO MembershipSettings (
                  Id, MembershipYearLabel, MembershipYearStart, MembershipYearEnd,
                  JoiningFeeFull, JoiningFeeHalf, RenewalFee, ReminderDaysBeforeExpiry,
                  CommitteeEmail, ClubName, ClubAbn, ClubPoBox, ClubPhone, ClubEmail,
                  LogoPath, UpdatedAt)
                VALUES (
                  1, '2026/2027', '2026-07-01', '2027-06-30',
                  65.00, 32.50, 0, 30,
                  'nickeltown@gmail.com',
                  'Nickeltown Flounderers Inc Auto Club',
                  '45 087 371 412',
                  'PO Box 31, Kambalda WA 6442',
                  '0410 065 002',
                  'nickeltown@gmail.com',
                  NULL,
                  datetime('now'))
                """);
        }

        foreach (var section in MembershipFormContentSeed.Sections)
        {
            conn.Execute(
                """
                INSERT INTO MembershipFormContent (SectionKey, Title, Body, SortOrder, UpdatedAt)
                SELECT @key, @title, @body, @sortOrder, datetime('now')
                WHERE NOT EXISTS (SELECT 1 FROM MembershipFormContent WHERE SectionKey = @key)
                """,
                new
                {
                    key = section.Key,
                    title = section.Title,
                    body = section.Body,
                    sortOrder = section.SortOrder,
                });
        }
    }
}

using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.ViewModels;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class ShoppingListEmailTests
{
    [Theory]
    [InlineData("person@example.com", true)]
    [InlineData("  person@example.com  ", true)]
    [InlineData("not-an-email", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("a@b", false)]
    public void EmailAddressValidation_IsValid(string? email, bool expected)
    {
        Assert.Equal(expected, EmailAddressValidation.IsValid(email));
    }

    [Fact]
    public void AppEmailConfig_ShoppingListDefaults_DoNotCopyStockRecipients()
    {
        var cfg = new AppEmailConfig
        {
            RecipientEmails = ["club@example.com", "me@example.com"],
        };

        Assert.False(cfg.ShoppingListSaturdayEmailEnabled);
        Assert.True(cfg.ShoppingListSkipWhenEmpty);
        Assert.Equal(string.Empty, cfg.ShoppingListRecipientEmail);
    }

    [Fact]
    public void SmtpEmailSender_ResolveRecipients_ExplicitListNeverMergesStockRecipients()
    {
        var cfg = new AppEmailConfig
        {
            RecipientEmails = ["club@example.com", "personal@example.com"],
        };

        var routed = SmtpEmailSender.ResolveRecipients(cfg, ["shopper@example.com"]);
        Assert.Equal(new[] { "shopper@example.com" }, routed);

        var stockDefault = SmtpEmailSender.ResolveRecipients(cfg, toRecipients: null);
        Assert.Equal(2, stockDefault.Count);
        Assert.Contains("club@example.com", stockDefault);
    }

    [Fact]
    public void SmtpEmailSender_ResolveRecipients_ExplicitEmptyDoesNotFallBackToStock()
    {
        var cfg = new AppEmailConfig
        {
            RecipientEmails = ["club@example.com"],
        };

        var routed = SmtpEmailSender.ResolveRecipients(cfg, Array.Empty<string>());
        Assert.Empty(routed);
    }

    [Fact]
    public void ShoppingListCatalog_SeparatesBarAndMerchandiseByCategory()
    {
        var lager = Row(1, "Lager", "Beer", stock: 0, warn: 2, preferred: 12, pack: 6);
        var cola = Row(2, "Cola", "Drinks", stock: 1, warn: 4, preferred: 24, pack: 24);
        var hoodie = Row(3, "Club Hoodie", "Merch", stock: 2, warn: 3, preferred: 8, pack: 1);
        var pie = Row(4, "Meat Pie", "Food", stock: 0, warn: 2, preferred: 10, pack: 1);

        Assert.True(ShoppingListCatalog.IsBarShoppingCategory(lager));
        Assert.True(ShoppingListCatalog.IsBarShoppingCategory(cola));
        Assert.False(ShoppingListCatalog.IsBarShoppingCategory(hoodie));
        Assert.False(ShoppingListCatalog.IsBarShoppingCategory(pie));

        Assert.True(ShoppingListCatalog.IsMerchandiseShoppingCategory(hoodie));
        Assert.False(ShoppingListCatalog.IsMerchandiseShoppingCategory(lager));
        Assert.False(ShoppingListCatalog.IsMerchandiseShoppingCategory(pie));
    }

    [Fact]
    public void ShoppingListSnapshot_BarExcludesMerchAndFood_AndNeverRecommendsZero()
    {
        var rows = new List<StockEditorRow>
        {
            Row(1, "Lager", "Beer", stock: 0, warn: 2, preferred: 12, pack: 6),
            Row(2, "Plenty Stock", "Beer", stock: 40, warn: 2, preferred: 12, pack: 6),
            Row(3, "Club Cap", "Merch", stock: 1, warn: 3, preferred: 10, pack: 1),
            Row(4, "Banner", "Merch", stock: 0, warn: 1, preferred: 2, pack: 1),
            Row(5, "Broken Reorder", "Spirits", stock: 0, warn: 0, preferred: 0, pack: 6),
            Row(6, "Soft Drink", "Drinks", stock: 3, warn: 6, preferred: 24, pack: 0),
        };

        var bar = ShoppingListSnapshot.Build(rows, ShoppingListKind.Bar);
        Assert.Equal(2, bar.PurchaseRows.Count);
        Assert.Contains(bar.PurchaseRows, r => r.Name == "Lager");
        Assert.Contains(bar.PurchaseRows, r => r.Name == "Soft Drink");
        Assert.DoesNotContain(bar.PurchaseRows, r => r.Name == "Club Cap");
        Assert.DoesNotContain(bar.PurchaseRows, r => r.Name == "Banner");
        Assert.DoesNotContain(bar.PurchaseRows, r => r.UnitsToBuy <= 0);
        Assert.Contains("Broken Reorder", bar.InvalidReorderNames);
        Assert.Contains("Soft Drink", bar.MissingPackSizeNames);

        var lager = bar.PurchaseRows.Single(r => r.Name == "Lager");
        Assert.Equal(12, lager.UnitsToBuy);
        Assert.Equal(2, lager.PacksToBuy);

        var merch = ShoppingListSnapshot.Build(rows, ShoppingListKind.Merchandise);
        Assert.Equal(2, merch.PurchaseRows.Count);
        Assert.Contains(merch.PurchaseRows, r => r.Name == "Club Cap");
        Assert.Contains(merch.PurchaseRows, r => r.Name == "Banner");
        Assert.DoesNotContain(merch.PurchaseRows, r => r.Name == "Lager");
        Assert.DoesNotContain(merch.PurchaseRows, r => r.Name == "Soft Drink");
    }

    [Fact]
    public void ShoppingListEmailComposer_BodyContainsBuyNowAndAttachmentNote()
    {
        var source = Row(1, "Lager", "Beer", stock: 1, warn: 4, preferred: 12, pack: 6);
        Assert.True(StockInventoryLevelHelper.NeedsBuying(source));

        var snap = ShoppingListSnapshot.Build([source], ShoppingListKind.Bar);
        var body = ShoppingListEmailComposer.BuildEmailBody(snap, isSaturdaySchedule: true);
        Assert.Contains("BUY NOW", body, StringComparison.Ordinal);
        Assert.Contains("Lager", body, StringComparison.Ordinal);
        Assert.Contains("PDF shopping list is attached", body, StringComparison.Ordinal);
        Assert.Contains("Weekly Bar Shopping List", body, StringComparison.Ordinal);
        Assert.Contains("Saturday schedule", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ShoppingListEmailComposer_EmptyConfirmation_Readable()
    {
        var body = ShoppingListEmailComposer.BuildEmptyConfirmationBody(ShoppingListKind.Bar);
        Assert.Contains("Nothing needs buying", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Weekly Bar Shopping List", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ShoppingListPdfBuilder_BarAndMerch_AreSeparateDocuments()
    {
        var rows = new List<StockEditorRow>
        {
            Row(1, "Lager", "Beer", stock: 0, warn: 2, preferred: 12, pack: 6),
            Row(2, "Club Cap", "Merch", stock: 3, warn: 5, preferred: 10, pack: 0),
        };

        var barPdf = ShoppingListEmailComposer.BuildPdf(ShoppingListSnapshot.Build(rows, ShoppingListKind.Bar));
        var merchPdf = ShoppingListEmailComposer.BuildPdf(ShoppingListSnapshot.Build(rows, ShoppingListKind.Merchandise));

        AssertPdfMagic(barPdf);
        AssertPdfMagic(merchPdf);
        Assert.NotEqual(barPdf.Length, merchPdf.Length);

        var barText = System.Text.Encoding.Latin1.GetString(barPdf);
        var merchText = System.Text.Encoding.Latin1.GetString(merchPdf);
        Assert.Contains("Weekly Bar Shopping List", barText, StringComparison.Ordinal);
        Assert.DoesNotContain("Club Cap", barText, StringComparison.Ordinal);
        Assert.Contains("Merchandise Shopping List", merchText, StringComparison.Ordinal);
        Assert.DoesNotContain("Lager", merchText, StringComparison.Ordinal);
    }

    [Fact]
    public void SaturdayEmailClaim_FlagPathsDifferForStockAndShoppingList()
    {
        var saturday = new DateTime(2026, 10, 17); // known Saturday
        var stock = SaturdayEmailClaim.GetFlagPathOrNull(SaturdayEmailClaim.StockKind, saturday);
        var shop = SaturdayEmailClaim.GetFlagPathOrNull(SaturdayEmailClaim.ShoppingListKind, saturday);
        Assert.NotNull(stock);
        Assert.NotNull(shop);
        Assert.NotEqual(stock, shop);
        Assert.Contains("saturday_weekly_stock_email_", stock, StringComparison.Ordinal);
        Assert.Contains("saturday_shopping_list_email_", shop, StringComparison.Ordinal);
    }

    [Fact]
    public void SaturdayEmailClaim_NonSaturday_AllowsClaimWithoutFlag()
    {
        var monday = new DateTime(2026, 10, 12);
        Assert.Null(SaturdayEmailClaim.GetFlagPathOrNull(SaturdayEmailClaim.StockKind, monday));
        Assert.True(SaturdayEmailClaim.TryClaim(SaturdayEmailClaim.ShoppingListKind, monday));
    }

    [Fact]
    public async Task ShoppingListEmailService_Disabled_SkipsWithoutSending()
    {
        var email = new CapturingEmailSender();
        var cfg = new MemoryEmailConfig(new AppEmailConfig
        {
            ShoppingListSaturdayEmailEnabled = false,
            ShoppingListRecipientEmail = "shop@example.com",
        });
        var svc = new ShoppingListEmailService(
            new FakeStock([]),
            cfg,
            email,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ShoppingListEmailService>.Instance);

        var result = await svc.TrySendScheduledAsync();
        Assert.True(result.Skipped);
        Assert.Empty(email.Sends);
    }

    [Fact]
    public async Task ShoppingListEmailService_EmptySkip_DoesNotSend()
    {
        var email = new CapturingEmailSender();
        var cfg = new MemoryEmailConfig(new AppEmailConfig
        {
            ShoppingListSaturdayEmailEnabled = true,
            ShoppingListSkipWhenEmpty = true,
            ShoppingListRecipientEmail = "shop@example.com",
        });
        var svc = new ShoppingListEmailService(
            new FakeStock([]),
            cfg,
            email,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ShoppingListEmailService>.Instance);

        var result = await svc.TrySendScheduledAsync();
        Assert.True(result.Succeeded);
        Assert.True(result.Skipped);
        Assert.Empty(email.Sends);
    }

    [Fact]
    public async Task ShoppingListEmailService_EmptyConfirm_SendsWithoutPdf()
    {
        var email = new CapturingEmailSender();
        var cfg = new MemoryEmailConfig(new AppEmailConfig
        {
            ShoppingListSaturdayEmailEnabled = true,
            ShoppingListSkipWhenEmpty = false,
            ShoppingListRecipientEmail = "shop@example.com",
            RecipientEmails = ["club@example.com"],
        });
        var svc = new ShoppingListEmailService(
            new FakeStock([]),
            cfg,
            email,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ShoppingListEmailService>.Instance);

        var result = await svc.TrySendScheduledAsync();
        Assert.True(result.Succeeded);
        Assert.False(result.Skipped);
        Assert.Single(email.Sends);
        Assert.Equal(new[] { "shop@example.com" }, email.Sends[0].To);
        Assert.Null(email.Sends[0].Attachments);
    }

    [Fact]
    public async Task ShoppingListEmailService_SendsOnlyBarList_NeverMerch()
    {
        var email = new CapturingEmailSender();
        var cfg = new MemoryEmailConfig(new AppEmailConfig
        {
            ShoppingListSaturdayEmailEnabled = true,
            ShoppingListSkipWhenEmpty = true,
            ShoppingListRecipientEmail = "shop@example.com",
            RecipientEmails = ["club@example.com", "personal@example.com"],
        });
        var svc = new ShoppingListEmailService(
            new FakeStock(
            [
                Row(9, "Lager", "Beer", stock: 0, warn: 2, preferred: 12, pack: 6),
                Row(10, "Club Cap", "Merch", stock: 0, warn: 2, preferred: 8, pack: 1),
            ]),
            cfg,
            email,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ShoppingListEmailService>.Instance);

        var result = await svc.TrySendScheduledAsync();
        Assert.True(result.Succeeded);
        Assert.False(result.Skipped);
        Assert.Single(email.Sends);
        Assert.Equal(new[] { "shop@example.com" }, email.Sends[0].To);
        Assert.DoesNotContain("club@example.com", email.Sends[0].To);
        Assert.NotNull(email.Sends[0].Attachments);
        Assert.Single(email.Sends[0].Attachments!);
        Assert.Contains("Bar_Shopping_List", email.Sends[0].Attachments![0].FileName, StringComparison.Ordinal);
        Assert.Contains("bar shopping list", email.Sends[0].Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lager", email.Sends[0].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Club Cap", email.Sends[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShoppingListEmailService_SendFailure_ReturnsFailedWithoutStockClaim()
    {
        var cfg = new MemoryEmailConfig(new AppEmailConfig
        {
            ShoppingListSaturdayEmailEnabled = true,
            ShoppingListRecipientEmail = "shop@example.com",
        });
        var svc = new ShoppingListEmailService(
            new FakeStock(
            [
                Row(1, "Lager", "Beer", stock: 0, warn: 2, preferred: 12, pack: 6),
            ]),
            cfg,
            new FailingEmailSender(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ShoppingListEmailService>.Instance);

        var result = await svc.TrySendScheduledAsync();
        Assert.False(result.Succeeded);
        Assert.False(SaturdayEmailClaim.HasClaim(SaturdayEmailClaim.StockKind, DateTime.Now));
    }

    [Fact]
    public async Task EmailConfigService_PersistsShoppingListSettings()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ntpos_emailcfg_{Guid.NewGuid():N}.db");
        try
        {
            var factory = new SqliteConnectionFactory(dbPath);
            await new DatabaseInitializer(factory).InitializeAsync();
            var settings = new SqliteAppSettingsRepository(
                factory,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SqliteAppSettingsRepository>.Instance);
            var svc = new EmailConfigService(settings);

            await svc.SaveAsync(new AppEmailConfig
            {
                RecipientEmails = ["club@example.com"],
                ShoppingListRecipientEmail = "shop@example.com",
                ShoppingListSaturdayEmailEnabled = true,
                ShoppingListSkipWhenEmpty = false,
            });

            var loaded = await svc.LoadAsync();
            Assert.Equal(["club@example.com"], loaded.RecipientEmails);
            Assert.Equal("shop@example.com", loaded.ShoppingListRecipientEmail);
            Assert.True(loaded.ShoppingListSaturdayEmailEnabled);
            Assert.False(loaded.ShoppingListSkipWhenEmpty);
        }
        finally
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    private static StockEditorRow Row(
        long id,
        string name,
        string subCategory,
        int stock,
        int warn,
        int preferred,
        int pack) =>
        new()
        {
            ItemId = id,
            Name = name,
            IsActive = 1,
            TrackStock = 1,
            StockQty = stock,
            WarnMeBelow = warn > 0 ? warn : null,
            PreferredStockLevel = preferred > 0 ? preferred : null,
            PurchaseUnitQty = pack > 0 ? pack : null,
            CatalogSubCategory = subCategory,
            StockMode = StockCatalogTaxonomy.StockModeTracked,
            ShowOnShoppingList = 1,
        };

    private static void AssertPdfMagic(byte[] pdf)
    {
        Assert.True(pdf.Length > 4);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<(string Subject, string Body, IReadOnlyCollection<string> To, IReadOnlyCollection<EmailAttachment>? Attachments)> Sends { get; } = [];

        public Task SendAsync(
            string subject,
            string body,
            IReadOnlyCollection<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default)
        {
            Sends.Add((subject, body, Array.Empty<string>(), attachments));
            return Task.CompletedTask;
        }

        public Task SendToAsync(
            string subject,
            string body,
            IReadOnlyCollection<string> toRecipients,
            IReadOnlyCollection<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default)
        {
            Sends.Add((subject, body, toRecipients.ToList(), attachments));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(
            string subject,
            string body,
            IReadOnlyCollection<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("SMTP down");

        public Task SendToAsync(
            string subject,
            string body,
            IReadOnlyCollection<string> toRecipients,
            IReadOnlyCollection<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("SMTP down");
    }

    private sealed class MemoryEmailConfig : IEmailConfigService
    {
        private AppEmailConfig _cfg;

        public MemoryEmailConfig(AppEmailConfig cfg) => _cfg = cfg;

        public Task<AppEmailConfig> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_cfg);

        public Task SaveAsync(AppEmailConfig config, CancellationToken cancellationToken = default)
        {
            _cfg = config;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStock : IStockEditingService
    {
        private readonly IReadOnlyList<StockEditorRow> _rows;

        public FakeStock(IReadOnlyList<StockEditorRow> rows) => _rows = rows;

        public Task<IReadOnlyList<StockEditorRow>> GetStockRowsAsync(bool includeInactive = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rows);

        public Task<IReadOnlyList<StockCategoryRow>> GetStockCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StockCategoryRow> CreateStockCategoryAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateStockCategoryAsync(long categoryId, string newName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MoveStockCategoryAsync(long categoryId, int direction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SoftDeleteStockCategoryAsync(long categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountOtherItemsWithSkuAsync(long excludeItemId, string sku, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountOtherItemsMatchingScanCodeAsync(long excludeItemId, string scanCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> CreateStockItemAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpsertLatestItemPriceAsync(long itemId, string priceKind, decimal price, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateStockRowAsync(long itemId, int newQty, int trackStock, long? categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ApplyPitstopEodDeductionAsync(long batchId, long itemId, int quantity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PermanentlyDeleteStockItemAsync(long itemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateItemAdminAsync(StockItemAdminWrite write, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReceiveStockAsync(StockPurchaseWrite purchase, int newStockQty, double costEach, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ApplyStockCountAsync(IReadOnlyList<(long ItemId, int NewQty)> counts, IReadOnlyList<long>? countedItemIds = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetLatestStockCountDateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task InsertStockPurchaseAsync(StockPurchaseWrite purchase, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

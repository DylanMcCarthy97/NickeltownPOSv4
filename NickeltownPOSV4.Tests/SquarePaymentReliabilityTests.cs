using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Payments;
using Xunit;

namespace NickeltownPOSV4.Tests;

[CollectionDefinition("PaymentReliability", DisableParallelization = true)]
public sealed class PaymentReliabilityCollection { }

[Collection("PaymentReliability")]
public sealed class SquarePaymentReliabilityTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ntpos_reliability_{Guid.NewGuid():N}.db");
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteSquarePaymentAttemptRepository _attempts;
    private readonly SqliteSquareRecoveryRepository _recovery;

    public SquarePaymentReliabilityTests()
    {
        _factory = new SqliteConnectionFactory(_path);
        new DatabaseInitializer(_factory).InitializeAsync().GetAwaiter().GetResult();
        _attempts = new(_factory);
        _recovery = new(_factory);
    }

    [Fact]
    public async Task ApprovedTopUp_LocalSaveFails_RetrySavesWithoutAnotherCharge()
    {
        using var db = _factory.OpenConnection();
        db.Execute("""
            INSERT INTO Tabs (LegacyId, Name, Balance, IsGuest, TabType, IsArchived, CreatedAt, UpdatedAt)
            VALUES ('tab1', 'Test', 0, 0, 'Member', 1, datetime('now'), datetime('now'))
            """);
        var terminal = new RecordingTerminal();
        var orchestrator = new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit());
        var request = Request("top-up");
        var first = await orchestrator.PresentAndLogAsync(request);
        var funds = new SqliteTabFundsService(_factory, _attempts);
        var metadata = new SquarePaymentCommitMetadata
        {
            IdempotencyKey = first.IdempotencyKey, SquarePaymentId = first.SquarePaymentId,
            SquareCheckoutId = first.SquareCheckoutId, BaseAmount = 50, ChargedAmount = 50,
            PaymentAttemptId = first.AttemptId,
        };
        var failed = await funds.CommitSquareCardTopUpAsync("tab1", 50, null, metadata);
        Assert.False(failed.Ok);
        Assert.Equal(1, await _recovery.GetUnresolvedCountAsync());

        db.Execute("UPDATE Tabs SET IsArchived = 0 WHERE LegacyId = 'tab1'");
        var retry = await orchestrator.PresentAndLogAsync(request);
        Assert.True(retry.Approved);
        Assert.Equal(first.SquarePaymentId, retry.SquarePaymentId);
        var saved = await funds.CommitSquareCardTopUpAsync("tab1", 50, null, metadata);
        Assert.True(saved.Ok, saved.ErrorMessage);
        var replay = await orchestrator.PresentAndLogAsync(request);
        Assert.True(replay.AlreadyRecorded);
        Assert.Equal(1, terminal.Calls);
        Assert.Equal(50m, db.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE LegacyId = 'tab1'"));
        Assert.Equal(1, db.ExecuteScalar<int>("SELECT COUNT(*) FROM Payments"));
        Assert.Equal(0, await _recovery.GetUnresolvedCountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentPanel_RetryRetainsKeyWhenLocalSaveFails(bool guestCloseout)
    {
        using var db = _factory.OpenConnection();
        db.Execute("""
            INSERT INTO Tabs (LegacyId, Name, Balance, IsGuest, TabType, IsArchived, CreatedAt, UpdatedAt)
            VALUES ('tab1', 'Test', -50, 1, 'Guest', 1, datetime('now'), datetime('now'))
            """);
        var session = new AddFundsSession(new TabPanelTargetState
            { TabLegacyId = "tab1", TabDisplayName = "Test", DisplayBalance = -50 });
        var terminal = new RecordingTerminal();
        var orchestrator = new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit());
        var funds = new SqliteTabFundsService(_factory, _attempts);
        CommunityToolkit.Mvvm.Input.IAsyncRelayCommand submit;
        Func<string> status;
        if (guestCloseout)
        {
            var vm = new NickeltownPOSV4.ViewModels.GuestTabCloseoutPanelViewModel(funds,
                null!, session, null!, null!, null!, null!, orchestrator, new Config());
            vm.PayCardCommand.Execute(null);
            submit = vm.ConfirmCardCommand;
            status = () => vm.StatusMessage;
        }
        else
        {
            var vm = new NickeltownPOSV4.ViewModels.AddFundsPanelViewModel(funds,
                session, null!, null!, null!, null!, orchestrator, new Config(), null!, new Audit(), null!, null!);
            vm.SelectedTile = vm.Types.Single(t => t.Key == "square");
            vm.AmountText = "50.00";
            submit = vm.ApplyCommand;
            status = () => vm.StatusMessage;
        }
        await Task.Delay(MoneyActionGuard.DefaultMilliseconds + 50);
        await submit.ExecuteAsync(null);
        Assert.Contains("Card approved", status(), StringComparison.Ordinal);
        await Task.Delay(MoneyActionGuard.DefaultMilliseconds + 50);
        await submit.ExecuteAsync(null);
        Assert.Contains("Card approved", status(), StringComparison.Ordinal);
        Assert.Equal(1, terminal.Calls);
        Assert.Single(await _recovery.GetOrphanAttemptsAsync());
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM Payments"));
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("cancel")]
    [InlineData("transport")]
    public async Task UncertainPayment_PersistsCheckout_AndBlocksRetryAfterRestart(string failure)
    {
        var terminal = new RecordingTerminal { Failure = failure };
        var orchestrator = new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit());
        var outcome = await orchestrator.PresentAndLogAsync(Request("original"));
        Assert.True(outcome.RequiresRecovery);
        Assert.False(outcome.Approved);
        var row = Assert.Single(await _recovery.GetOrphanAttemptsAsync());
        Assert.Equal("Unknown", row.Status);
        Assert.Equal("checkout-original", row.SquareCheckoutId);

        var restarted = new SquareCardPaymentOrchestrator(terminal,
            new SqliteSquarePaymentAttemptRepository(_factory), new Audit());
        Assert.True((await restarted.PresentAndLogAsync(Request("original"))).RequiresRecovery);
        Assert.True((await restarted.PresentAndLogAsync(Request("new-key"))).RequiresRecovery);
        Assert.Equal(1, terminal.Calls);

        await _recovery.MarkManuallyReconciledAsync(row.AttemptId, "Checked in Square; cancelled, no charge.");
        terminal.Failure = null;
        Assert.True((await restarted.PresentAndLogAsync(Request("replacement"))).Approved);
        Assert.Equal(2, terminal.Calls);
    }

    [Fact]
    public async Task PendingAttemptAfterCrash_IsVisible_AndBlocksAnotherCharge()
    {
        await _attempts.BeginAsync(new SquarePaymentAttemptBeginRequest
        {
            PaymentType = SquarePaymentAttemptType.TabTopUp, TabLegacyId = "tab1",
            IdempotencyKey = "crashed", BaseAmount = 50, ChargedAmount = 50,
        });
        var terminal = new RecordingTerminal();
        var orchestrator = new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit());
        Assert.True((await orchestrator.PresentAndLogAsync(Request("new-session"))).RequiresRecovery);
        Assert.Equal(0, terminal.Calls);
        Assert.Equal(1, await _recovery.GetUnresolvedCountAsync());
    }

    [Fact]
    public async Task DefinitiveDecline_AllowsNewAttempt_ButDoesNotReplayOldCheckout()
    {
        var terminal = new RecordingTerminal { Failure = "decline" };
        var orchestrator = new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit());
        var first = await orchestrator.PresentAndLogAsync(Request("declined"));
        Assert.False(first.Approved);
        Assert.False(first.RequiresRecovery);
        Assert.Equal(0, await _recovery.GetUnresolvedCountAsync());
        terminal.Failure = null;
        Assert.False((await orchestrator.PresentAndLogAsync(Request("declined"))).Approved);
        Assert.True((await orchestrator.PresentAndLogAsync(Request("retry"))).Approved);
        Assert.Equal(2, terminal.Calls);
    }

    [Fact]
    public async Task ApprovedPayment_NewKeyCannotBypassMissingLocalRecord()
    {
        var terminal = new RecordingTerminal();
        var orchestrator = new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit());
        await orchestrator.PresentAndLogAsync(Request("approved"));
        Assert.True((await orchestrator.PresentAndLogAsync(Request("edited-or-reopened"))).RequiresRecovery);
        Assert.Equal(1, terminal.Calls);
    }

    [Fact]
    public async Task IdempotencyKeyCannotBeReusedForDifferentAmountOrTab()
    {
        var terminal = new RecordingTerminal();
        var orchestrator = new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit());
        await orchestrator.PresentAndLogAsync(Request("same-key"));
        Assert.True((await orchestrator.PresentAndLogAsync(Request("same-key", amount: 60))).RequiresRecovery);
        Assert.True((await orchestrator.PresentAndLogAsync(Request("same-key", tab: "tab2"))).RequiresRecovery);
        Assert.Equal(1, terminal.Calls);
    }

    [Theory]
    [InlineData("COMPLETED", true, false)]
    [InlineData("CANCELED", false, false)]
    [InlineData("FAILED", false, false)]
    [InlineData("CANCEL_REQUESTED", false, true)]
    [InlineData("IN_PROGRESS", false, true)]
    [InlineData(null, false, true)]
    public void CancelRequestUsesFinalSquareStatus(string? status, bool approved, bool uncertain)
    {
        var result = SquareSdkTerminalSession.ResolveCancelledCheckout(status, "payment", "checkout", "key");
        Assert.Equal(approved, result.Approved);
        Assert.Equal(uncertain, result.RequiresRecovery);
        if (approved || uncertain) Assert.Equal("checkout", result.SquareCheckoutId);
    }

    [Fact]
    public async Task UpgradeSurfacesOldTimeouts_ButPreservesConfirmedDeclinesAndReconciledRows()
    {
        using var db = _factory.OpenConnection();
        foreach (var key in new[] { "old-timeout", "cancelled-on-reader", "reconciled" })
        {
            db.Execute("""
                INSERT INTO SquarePaymentAttempts
                  (IdempotencyKey, Status, PaymentType, BaseAmount, SurchargeAmount, ChargedAmount, FailureReason, RecoveryStatus, CreatedAt, UpdatedAt)
                VALUES (@key, 'Failed', 'TabTopUp', 50, 0, 50, @reason, @recovery, datetime('now'), datetime('now'))
                """, new
                {
                    key,
                    reason = key == "cancelled-on-reader" ? "Payment cancelled on Square Terminal." : "Square Terminal did not complete within 2 minutes.",
                    recovery = key == "reconciled" ? SquareRecoveryStatuses.ManuallyReconciled : null,
                });
        }
        var initializer = new DatabaseInitializer(_factory);
        await initializer.InitializeAsync();
        await initializer.InitializeAsync();
        var row = Assert.Single(await _recovery.GetOrphanAttemptsAsync());
        Assert.Equal("old-timeout", row.IdempotencyKey);
        Assert.Equal("Unknown", row.Status);
        Assert.Equal("Failed", db.ExecuteScalar<string>("SELECT Status FROM SquarePaymentAttempts WHERE IdempotencyKey = 'cancelled-on-reader'"));
        Assert.Equal(SquareRecoveryStatuses.ManuallyReconciled, db.ExecuteScalar<string>("SELECT RecoveryStatus FROM SquarePaymentAttempts WHERE IdempotencyKey = 'reconciled'"));
    }

    private static SquareCardPaymentRequest Request(string key, decimal amount = 50, string tab = "tab1")
    {
        var checkout = SquareTabCardCheckoutBuilder.Build(amount, 0, "Top-up", "Bar Tab", null, "Top-up", "BarTopUp", "Test");
        return SquareTabCardCheckoutBuilder.ToOrchestratorRequest(checkout, tab, key);
    }

    [Fact]
    public async Task TabCreditAndAttemptCompletionCommitTogether_OrBothRollBack()
    {
        using var db = _factory.OpenConnection();
        db.Execute("INSERT INTO Tabs (LegacyId, Name, Balance, IsGuest, TabType, CreatedAt, UpdatedAt) VALUES ('tab1', 'Test', 0, 0, 'Member', datetime('now'), datetime('now'))");
        var outcome = await new SquareCardPaymentOrchestrator(new RecordingTerminal(), _attempts, new Audit()).PresentAndLogAsync(Request("atomic-tab"));
        var metadata = new SquarePaymentCommitMetadata
        {
            IdempotencyKey = outcome.IdempotencyKey, SquarePaymentId = outcome.SquarePaymentId,
            PaymentAttemptId = outcome.AttemptId, BaseAmount = 50, ChargedAmount = 50,
        };
        db.Execute("CREATE TRIGGER block_completion BEFORE UPDATE OF Status ON SquarePaymentAttempts WHEN NEW.Status = 'Completed' BEGIN SELECT RAISE(ABORT, 'completion blocked'); END");
        var funds = new SqliteTabFundsService(_factory, _attempts);
        Assert.False((await funds.CommitSquareCardTopUpAsync("tab1", 50, null, metadata)).Ok);
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM Payments"));
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM MoneyMovements"));
        Assert.Equal(0m, db.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE LegacyId = 'tab1'"));
        Assert.Equal("TerminalApproved", Assert.Single(await _recovery.GetOrphanAttemptsAsync()).Status);
        db.Execute("DROP TRIGGER block_completion");
        Assert.True((await funds.CommitSquareCardTopUpAsync("tab1", 50, null, metadata)).Ok);
        Assert.True((await funds.CommitSquareCardTopUpAsync("tab1", 50, null, metadata)).Ok);
        Assert.Equal(50m, db.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE LegacyId = 'tab1'"));
        Assert.Equal(1, db.ExecuteScalar<int>("SELECT COUNT(*) FROM Payments"));
        Assert.Equal(0, await _recovery.GetUnresolvedCountAsync());
    }

    [Fact]
    public async Task PitstopSaleStockAndCompletionCommitTogether_OrAllRollBack()
    {
        using var db = _factory.OpenConnection();
        var itemId = db.ExecuteScalar<long>("INSERT INTO Items (Name, StockQty, TrackStock, IsActive, CatalogBucket, CatalogSubCategory, StockMode, CreatedAt, UpdatedAt) VALUES ('Cola', 10, 1, 1, 'Bar', 'Drinks', 'Tracked', datetime('now'), datetime('now')); SELECT last_insert_rowid()");
        var outcome = await new SquareCardPaymentOrchestrator(new RecordingTerminal(), _attempts, new Audit()).PresentAndLogAsync(new SquareCardPaymentRequest
        {
            IdempotencyKey = "atomic-pitstop", PaymentType = SquarePaymentAttemptType.PitstopSale,
            BaseAmount = 10, ChargedAmount = 10, TerminalRequest = new SquarePaymentRequest { TotalAmount = 10 },
        });
        var payment = new PitstopSalePaymentCommit
        {
            PaymentMethod = "Card", BaseProductTotal = 10, ChargedTotal = 10,
            IdempotencyKey = outcome.IdempotencyKey, SquareExternalRef = outcome.SquarePaymentId, PaymentAttemptId = outcome.AttemptId,
        };
        var lines = new[] { new PitstopSaleLineCommit { ItemId = itemId, DisplayName = "Cola", UnitPrice = 5, Quantity = 2 } };
        var sales = new SqlitePitstopRetailSaleRepository(_factory, _attempts);
        db.Execute("CREATE TRIGGER block_completion BEFORE UPDATE OF Status ON SquarePaymentAttempts WHEN NEW.Status = 'Completed' BEGIN SELECT RAISE(ABORT, 'completion blocked'); END");
        Assert.False((await sales.CommitSaleAsync(lines, payment)).Ok);
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM PitstopSales"));
        Assert.Equal(10, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @itemId", new { itemId }));
        db.Execute("DROP TRIGGER block_completion");
        Assert.True((await sales.CommitSaleAsync(lines, payment)).Ok);
        Assert.True((await sales.CommitSaleAsync(lines, payment)).Ok);
        Assert.Equal(1, db.ExecuteScalar<int>("SELECT COUNT(*) FROM PitstopSales"));
        Assert.Equal(8, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @itemId", new { itemId }));
        Assert.Equal(0, await _recovery.GetUnresolvedCountAsync());
    }

    [Fact]
    public async Task RecoveryChecksExistingCheckout_AndSavesApprovedTopUpOnce()
    {
        using var db = _factory.OpenConnection();
        db.Execute("INSERT INTO Tabs (LegacyId, Name, Balance, IsGuest, TabType, CreatedAt, UpdatedAt) VALUES ('tab1', 'Test', 0, 0, 'Member', datetime('now'), datetime('now'))");
        var terminal = new RecordingTerminal { Failure = "timeout" };
        var pending = await new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit()).PresentAndLogAsync(Request("status-check"));
        var reader = new StatusReader(SquarePresentResult.ApprovedSim("confirmed-payment", "checkout-status-check"));
        var service = new SquareRecoveryService(_recovery, _attempts, reader, new SqliteTabFundsService(_factory, _attempts));
        Assert.False((await service.SaveTabTopUpAsync(pending.AttemptId!.Value)).Ok);
        Assert.True((await service.RefreshStatusAsync(pending.AttemptId.Value)).Ok);
        Assert.Equal("checkout-status-check", reader.CheckoutId);
        Assert.True((await service.SaveTabTopUpAsync(pending.AttemptId.Value)).Ok);
        Assert.True((await service.SaveTabTopUpAsync(pending.AttemptId.Value)).Ok);
        Assert.Equal(1, terminal.Calls);
        Assert.Equal(50m, db.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE LegacyId = 'tab1'"));
        Assert.Equal(1, db.ExecuteScalar<int>("SELECT COUNT(*) FROM Payments"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryOnlyClosesAnUncertainPaymentWhenSquareConfirmsFailure(bool cancelled)
    {
        var terminal = new RecordingTerminal { Failure = "timeout" };
        var outcome = await new SquareCardPaymentOrchestrator(terminal, _attempts, new Audit()).PresentAndLogAsync(Request("check-pending"));
        var reader = new StatusReader(cancelled ? SquarePresentResult.Declined("Confirmed cancelled", cancelled: true) : SquarePresentResult.Uncertain("Still processing"));
        var service = new SquareRecoveryService(_recovery, _attempts, reader, new SqliteTabFundsService(_factory, _attempts));
        var result = await service.RefreshStatusAsync(outcome.AttemptId!.Value);
        Assert.Equal(cancelled, result.Ok);
        Assert.Equal(cancelled ? 0 : 1, await _recovery.GetUnresolvedCountAsync());
        Assert.Equal(1, terminal.Calls);
    }

    private sealed class StatusReader(SquarePresentResult result) : ISquareCheckoutStatusReader
    {
        public string? CheckoutId { get; private set; }
        public Task<SquarePresentResult> CheckStatusAsync(string checkoutId, decimal chargedAmount, CancellationToken cancellationToken = default)
        {
            CheckoutId = checkoutId;
            Assert.Equal(50m, chargedAmount);
            return Task.FromResult(result);
        }
    }

    [Theory]
    [InlineData(5000, true, true, true, true)]
    [InlineData(4000, true, true, true, false)]
    [InlineData(5000, false, true, true, false)]
    [InlineData(5000, true, false, true, false)]
    [InlineData(5000, true, true, false, false)]
    public void StatusCheckVerifiesAmountCurrencyCheckoutAndPaymentId(long cents, bool aud, bool matchingCheckout, bool hasPayment, bool approved)
    {
        var checkout = new Square.TerminalCheckout
        {
            Id = matchingCheckout ? "checkout" : "other-checkout",
            AmountMoney = new Square.Money { Amount = cents, Currency = aud ? Square.Currency.Aud : Square.Currency.Usd },
            DeviceOptions = new Square.DeviceCheckoutOptions { DeviceId = "device" },
            Status = "COMPLETED",
            PaymentIds = hasPayment ? new[] { "payment" } : Array.Empty<string>(),
        };
        var result = SquareSdkTerminalSession.ResolveCheckedCheckout(checkout, 50, "checkout");
        Assert.Equal(approved, result.Approved);
        Assert.Equal(!approved, result.RequiresRecovery);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }

    private sealed class RecordingTerminal : ISquareTerminalSession
    {
        public int Calls { get; private set; }
        public string? Failure { get; set; }
        public Task<SquarePresentResult> PresentCardChargeAsync(decimal amount, CancellationToken cancellationToken = default, string? checkoutNote = null) => throw new NotSupportedException();
        public async Task<SquarePresentResult> PresentPaymentRequestAsync(SquarePaymentRequest paymentRequest,
            CancellationToken cancellationToken = default, string? idempotencyKey = null, Func<string, Task>? checkoutCreated = null)
        {
            Calls++;
            var checkoutId = "checkout-" + idempotencyKey;
            if (checkoutCreated is not null) await checkoutCreated(checkoutId);
            return Failure switch
            {
                "timeout" => SquarePresentResult.Declined("Timed out", timedOut: true),
                "cancel" => throw new OperationCanceledException(),
                "transport" => throw new IOException("Connection lost"),
                "decline" => SquarePresentResult.Declined("Cancelled on terminal", cancelled: true),
                _ => SquarePresentResult.ApprovedSim("payment-" + idempotencyKey, checkoutId, idempotencyKey),
            };
        }
    }

    private sealed class Config : NickeltownPOSV4.Services.Settings.ISquareConfigService
    {
        public Task<NickeltownPOSV4.Models.Settings.AppSquareConfig> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new NickeltownPOSV4.Models.Settings.AppSquareConfig());
        public Task SaveAsync(NickeltownPOSV4.Models.Settings.AppSquareConfig config, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Audit : IAuditLogService
    {
        public Task<long> LogAsync(AuditLogEntryRequest request, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<long> LogAsync(string actionType, string? entityType = null, string? entityId = null, decimal? amount = null, string? reason = null, bool success = true, string? detailsJson = null, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int maxEntries = 400, bool staffFacingOnly = true, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuditLogEntry>>([]);
    }
}

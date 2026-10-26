using System.ComponentModel;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Logging;
using NickeltownPOSV4.Themes;
using NickeltownPOSV4.ViewModels;
using Xunit;

namespace NickeltownPOSV4.Tests;

[Collection("LoginTransitionProbe")]
public sealed class LoginTransitionTests
{
    [Fact]
    public void Probe_RecordsStagesAndTotal()
    {
        LoginTransitionProbe.Begin("unit");
        LoginTransitionProbe.Mark("auth.ok");
        var summary = LoginTransitionProbe.Complete("done");

        Assert.Contains("BEGIN unit", LoginTransitionProbe.LastLines[0], StringComparison.Ordinal);
        Assert.Contains("auth.ok", string.Join('\n', LoginTransitionProbe.LastLines), StringComparison.Ordinal);
        Assert.Contains("LoginTransition total", summary, StringComparison.Ordinal);
        Assert.Equal(summary, LoginTransitionProbe.LastSummary);

        LoginTransitionProbe.Mark("after.complete.ignored");
        Assert.DoesNotContain("after.complete.ignored", string.Join('\n', LoginTransitionProbe.LastLines), StringComparison.Ordinal);
        Assert.Equal(summary, LoginTransitionProbe.Complete("second.complete.ignored"));
    }

    [Fact]
    public async Task SuccessfulPin_SetsSessionAndNavigatesToMainShell()
    {
        var auth = new GateAuth();
        var session = new RecordingSession();
        var nav = new FakeNav();
        var themes = new FakeThemes();
        var vm = new LoginViewModel(auth, session, nav, themes);

        auth.Result = AuthenticationResult.Success(7, "dylan", "Dylan", "admin", "Light");
        EnterPin(vm, "1234");
        await WaitUntil(() => nav.MainShell == 1);

        Assert.Equal(1, auth.Calls);
        Assert.True(session.IsSignedIn);
        Assert.Equal(7, session.ActiveStaffId);
        Assert.Equal("Dylan", session.DisplayName);
        Assert.Equal(1, nav.MainShell);
        Assert.Equal(0, nav.Forced);
        Assert.Equal(0, nav.Login);
        Assert.False(vm.IsBusy);
        // The real MainShell completes the probe on load; this navigation fake does not load a page.
        Assert.Contains("auth.ok", string.Join('\n', LoginTransitionProbe.LastLines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedPin_DoesNotGrantAccess()
    {
        var auth = new GateAuth();
        var session = new RecordingSession();
        var nav = new FakeNav();
        var vm = new LoginViewModel(auth, session, nav, new FakeThemes());

        auth.Result = AuthenticationResult.Fail("PIN not recognized.");
        EnterPin(vm, "9999");
        await WaitUntil(() => vm.HasStatusMessage);

        Assert.False(session.IsSignedIn);
        Assert.Equal(0, nav.MainShell);
        Assert.Equal("PIN not recognized.", vm.StatusMessage);
    }

    [Fact]
    public async Task DuplicateSubmit_WhileAuthRunning_IsIgnored()
    {
        var auth = new GateAuth { Hold = true };
        var session = new RecordingSession();
        var nav = new FakeNav();
        var vm = new LoginViewModel(auth, session, nav, new FakeThemes());

        EnterPin(vm, "1234");
        await WaitUntil(() => auth.Calls == 1);
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanEnterDigits);

        await vm.SubmitCommand.ExecuteAsync(null);
        vm.NumpadDigitCommand.Execute("1");
        Assert.Equal(1, auth.Calls);
        Assert.Equal(0, nav.MainShell);

        auth.Release(AuthenticationResult.Success(1, "a", "Ann", "staff"));
        await WaitUntil(() => nav.MainShell == 1);
        Assert.Equal(1, auth.Calls);
        Assert.True(session.IsSignedIn);
    }

    [Fact]
    public async Task RequiresPinChange_DoesNotOpenMainShell()
    {
        var auth = new GateAuth();
        var session = new RecordingSession();
        var nav = new FakeNav();
        var vm = new LoginViewModel(auth, session, nav, new FakeThemes());

        auth.Result = AuthenticationResult.Success(1, "admin", "Admin", "admin", requiresPinChange: true);
        EnterPin(vm, "1234");
        await WaitUntil(() => nav.Forced == 1);

        Assert.True(session.IsSignedIn);
        Assert.Equal(0, nav.MainShell);
        Assert.Equal(1, nav.Forced);
    }

    private static void EnterPin(LoginViewModel vm, string pin)
    {
        foreach (var ch in pin)
        {
            vm.NumpadDigitCommand.Execute(ch.ToString());
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 80; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail("Timed out waiting for login transition.");
    }

    private sealed class GateAuth : IAuthenticationService
    {
        private readonly TaskCompletionSource<AuthenticationResult> _hold = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public bool Hold { get; set; }

        public AuthenticationResult Result { get; set; } = AuthenticationResult.Fail("unset");

        public Task<AuthenticationResult> AuthenticateByPinAsync(
            string pin,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Hold ? _hold.Task : Task.FromResult(Result);
        }

        public void Release(AuthenticationResult result) => _hold.TrySetResult(result);
    }

    private sealed class FakeNav : IRootNavigationCoordinator
    {
        public int MainShell { get; private set; }

        public int Login { get; private set; }

        public int Forced { get; private set; }

        public void Attach(Microsoft.UI.Xaml.Controls.Frame rootFrame)
        {
        }

        public void NavigateToStartup()
        {
        }

        public void NavigateToLogin() => Login++;

        public void NavigateToForcedPinChange() => Forced++;

        public void NavigateToMainShell() => MainShell++;
    }

    private sealed class FakeThemes : IPosThemeService
    {
        public UiThemeId LastApplied { get; private set; } = UiThemeId.Light;

        public UiThemeId CurrentThemeId => LastApplied;

        public event EventHandler? ThemeChanged;

        public IReadOnlyList<UiThemeId> AllThemeIds => [UiThemeId.Light];

        public IReadOnlyList<UiThemeId> RecommendedThemeIds => AllThemeIds;

        public IReadOnlyList<UiThemeId> MoreThemeIds => [];

        public string GetDisplayName(UiThemeId id) => id.ToString();

        public void Apply(UiThemeId themeId)
        {
            LastApplied = themeId;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        public void PushBrushesToApplicationResources()
        {
        }
    }

    private sealed class RecordingSession : IUserSessionService
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsSignedIn { get; private set; }

        public long? ActiveStaffId { get; private set; }

        public string? ActiveStaffLegacyId { get; private set; }

        public string? DisplayName { get; private set; }

        public string? Role { get; private set; }

        public bool IsAdmin => string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase);

        public bool IsTreasurer => false;

        public bool IsManager => IsAdmin;

        public bool CanAccessAdmin => IsAdmin;

        public bool CanAccessReports => IsAdmin;

        public bool CanAccessTreasurer => IsAdmin;

        public bool IsDeveloper { get; private set; }

        public void SetSignedIn(long staffPk, string? legacyId, string displayName, string? role, bool isDeveloper = false)
        {
            ActiveStaffId = staffPk;
            ActiveStaffLegacyId = legacyId;
            DisplayName = displayName;
            Role = role;
            IsDeveloper = isDeveloper;
            IsSignedIn = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        public void Clear()
        {
            IsSignedIn = false;
            ActiveStaffId = null;
            DisplayName = null;
            Role = null;
            IsDeveloper = false;
        }
    }
}

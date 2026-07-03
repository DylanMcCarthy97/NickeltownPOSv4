using System;
using System.ComponentModel;
using Microsoft.UI.Dispatching;

namespace NickeltownPOSV4.Services;

public sealed class SessionInactivityService : ISessionInactivityService
{
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(5);

    private readonly IUserSessionService _session;
    private readonly IAuthSignOutService _signOut;
    private DispatcherQueue? _dispatcher;
    private DispatcherQueueTimer? _timer;
    private DateTimeOffset _lastActivityUtc = DateTimeOffset.UtcNow;
    private bool _signOutInProgress;

    public SessionInactivityService(IUserSessionService session, IAuthSignOutService signOut)
    {
        _session = session;
        _signOut = signOut;
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    public TimeSpan IdleTimeout { get; } = DefaultIdleTimeout;

    public void Start(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        EnsureTimer();
        SyncMonitoringState();
    }

    public void NotifyActivity()
    {
        if (!_session.IsSignedIn)
        {
            return;
        }

        _lastActivityUtc = DateTimeOffset.UtcNow;
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IUserSessionService.IsSignedIn))
        {
            SyncMonitoringState();
        }
    }

    private void SyncMonitoringState()
    {
        if (_session.IsSignedIn)
        {
            _lastActivityUtc = DateTimeOffset.UtcNow;
            _timer?.Start();
            return;
        }

        _signOutInProgress = false;
        _timer?.Stop();
    }

    private void EnsureTimer()
    {
        if (_dispatcher is null || _timer is not null)
        {
            return;
        }

        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(15);
        _timer.Tick += OnTimerTick;
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!_session.IsSignedIn || _signOutInProgress)
        {
            return;
        }

        if (DateTimeOffset.UtcNow - _lastActivityUtc < IdleTimeout)
        {
            return;
        }

        _signOutInProgress = true;
        _timer?.Stop();
        _signOut.SignOutForInactivity();
    }
}
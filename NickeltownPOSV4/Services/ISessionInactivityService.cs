using System;
using Microsoft.UI.Dispatching;

namespace NickeltownPOSV4.Services;

/// <summary>Tracks user activity and signs out after a period of idle time.</summary>
public interface ISessionInactivityService
{
    TimeSpan IdleTimeout { get; }

    void Start(DispatcherQueue dispatcher);

    void NotifyActivity();
}
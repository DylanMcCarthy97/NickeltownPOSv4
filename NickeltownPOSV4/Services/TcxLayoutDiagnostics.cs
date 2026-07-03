using System;
using Microsoft.UI.Dispatching;

namespace NickeltownPOSV4.Services;

/// <summary>UI-thread marshalling helper (e.g. login PIN clear after async auth).</summary>
public static class TcxLayoutDiagnostics
{
    private static DispatcherQueue? _uiQueue;

    public static void SetUiDispatcher(DispatcherQueue queue) => _uiQueue = queue;

    /// <summary>Marshals work to the UI thread (used after <c>await</c> continuations that may not be on the UI thread).</summary>
    public static bool TryEnqueueNormal(Action callback)
    {
        if (_uiQueue is null)
        {
            callback();
            return true;
        }

        return _uiQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => callback());
    }
}

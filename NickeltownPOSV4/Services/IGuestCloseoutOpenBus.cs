using System;

namespace NickeltownPOSV4.Services;

/// <summary>Signals the tabs workspace to open the guest closeout slide panel.</summary>
public interface IGuestCloseoutOpenBus
{
    event EventHandler? OpenRequested;

    void RequestOpen();
}

using System;

namespace NickeltownPOSV4.Services;

public sealed class GuestCloseoutOpenBus : IGuestCloseoutOpenBus
{
    public event EventHandler? OpenRequested;

    public void RequestOpen() => OpenRequested?.Invoke(this, EventArgs.Empty);
}

using System;

namespace NickeltownPOSV4.Services;

/// <summary>Singleton bridge: <see cref="AddDrinksPanelViewModel"/> requests exit; <see cref="TabsWorkspaceViewModel"/> handles UI.</summary>
public sealed class AddDrinksWorkspaceNavigator : IAddDrinksWorkspaceNavigator
{
    private Action? _onRequestClose;

    public void SetHandler(Action? onRequestClose) => _onRequestClose = onRequestClose;

    public void RequestClose() => _onRequestClose?.Invoke();
}

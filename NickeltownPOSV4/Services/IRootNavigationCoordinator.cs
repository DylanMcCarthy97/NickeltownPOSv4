namespace NickeltownPOSV4.Services;



/// <summary>Root <see cref="Microsoft.UI.Xaml.Controls.Frame"/> navigation (login vs main shell).</summary>

public interface IRootNavigationCoordinator

{

    void Attach(Microsoft.UI.Xaml.Controls.Frame rootFrame);



    void NavigateToStartup();



    void NavigateToLogin();



    void NavigateToForcedPinChange();



    void NavigateToMainShell();

}


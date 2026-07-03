using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Controls;

namespace NickeltownPOSV4.Services;

public interface ISlidePanelService
{
    void Attach(SlidePanelHost host);

    /// <summary>Opens the slide panel. Optional width widens chrome (e.g. Add Drinks on TCxWave 1024×768).</summary>
    void Open(UserControl content, double? panelWidthPixels = null);

    void Close();

    bool IsOpen { get; }
}

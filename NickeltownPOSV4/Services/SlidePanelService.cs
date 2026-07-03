using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Controls;

namespace NickeltownPOSV4.Services;

public sealed class SlidePanelService : ISlidePanelService
{
    private SlidePanelHost? _host;

    public bool IsOpen => _host?.IsPanelOpen == true;

    public void Attach(SlidePanelHost host)
    {
        _host = host;
    }

    public void Open(UserControl content, double? panelWidthPixels = null)
    {
        _host?.Open(content, panelWidthPixels);
    }

    public void Close()
    {
        _host?.Close();
    }
}

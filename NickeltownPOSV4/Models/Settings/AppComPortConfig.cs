namespace NickeltownPOSV4.Models.Settings;

/// <summary>Serial port + baud used by the cash-drawer kick service.</summary>
public sealed class AppComPortConfig
{
    public string PortName { get; set; } = "COM1";

    public int BaudRate { get; set; } = 9600;
}

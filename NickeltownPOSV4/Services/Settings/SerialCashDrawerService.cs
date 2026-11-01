using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public sealed class SerialCashDrawerService : ISerialCashDrawerService
{
    private readonly IComPortConfigService _config;

    public SerialCashDrawerService(IComPortConfigService config) => _config = config;

    public async Task KickAsync(CancellationToken cancellationToken = default)
    {
        var config = await _config.LoadAsync(cancellationToken).ConfigureAwait(false);
        var portName = (config.PortName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(portName))
        {
            throw new InvalidOperationException(
                "The cash drawer is not set up. Open Admin → Setup → Cash drawer.");
        }

        try
        {
            await Task.Run(
                () =>
                {
                    using var port = new SerialPort(portName, config.BaudRate, Parity.None, 8, StopBits.One);
                    port.Open();
                    // ESC p 0 25 250 — standard Epson drawer-kick pulse.
                    byte[] command = [0x1B, 0x70, 0x00, 0x19, 0xFA];
                    port.Write(command, 0, command.Length);
                    Thread.Sleep(100);
                },
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Something else is using the cash drawer. Close other till programs and try again.");
        }
        catch (IOException)
        {
            throw new InvalidOperationException(
                "The cash drawer is not connected. Check the cable, then open Admin → Setup → Cash drawer.");
        }
    }
}

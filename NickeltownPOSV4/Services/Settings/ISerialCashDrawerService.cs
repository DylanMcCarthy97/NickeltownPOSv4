using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public interface ISerialCashDrawerService
{
    /// <summary>Sends the ESC p 0 25 250 pulse on the configured COM port to fire the drawer solenoid.</summary>
    Task KickAsync(CancellationToken cancellationToken = default);
}

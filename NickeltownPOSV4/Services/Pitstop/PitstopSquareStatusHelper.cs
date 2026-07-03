using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services.Pitstop;

internal static class PitstopSquareStatusHelper
{
    public static async Task<string> LoadStatusPillTextAsync(
        ISquareConfigService squareConfig,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sq = await squareConfig.LoadAsync(cancellationToken).ConfigureAwait(false);
            var env = string.Equals(sq.Environment, "sandbox", StringComparison.OrdinalIgnoreCase) ? "Sandbox" : "Live";
            var hasDevice = !string.IsNullOrWhiteSpace(sq.DeviceId);
            var hasToken = !string.IsNullOrWhiteSpace(sq.AccessToken);
            return hasDevice && hasToken ? $"Square · {env}" : "Square · not configured";
        }
        catch
        {
            return "Square";
        }
    }
}

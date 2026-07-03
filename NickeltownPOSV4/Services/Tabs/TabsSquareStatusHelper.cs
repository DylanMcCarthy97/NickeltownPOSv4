using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsSquareStatusHelper
{
    public static async Task<(string Label, bool IsOnline)> LoadFooterStatusAsync(
        ISquareConfigService squareConfig,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sq = await squareConfig.LoadAsync(cancellationToken).ConfigureAwait(false);
            var hasDevice = !string.IsNullOrWhiteSpace(sq.DeviceId);
            var hasToken = !string.IsNullOrWhiteSpace(sq.AccessToken);
            if (hasDevice && hasToken)
            {
                return ("Online (Square)", true);
            }

            return ("Square · not configured", false);
        }
        catch
        {
            return ("Square", false);
        }
    }
}
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Services.Migration;

public interface ILegacyJsonFileDetector
{
    Task<LegacyJsonDetectionResult> DetectAsync(string rootFolder, CancellationToken cancellationToken = default);
}

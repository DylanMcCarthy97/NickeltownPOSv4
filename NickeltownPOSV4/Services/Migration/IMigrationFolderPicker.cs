using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Migration;

public interface IMigrationFolderPicker
{
    Task<string?> PickV2DataFolderAsync();
}

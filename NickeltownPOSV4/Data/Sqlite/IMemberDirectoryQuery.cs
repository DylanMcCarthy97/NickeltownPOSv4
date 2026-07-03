using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Data.Sqlite;

public interface IMemberDirectoryQuery
{
    Task<IReadOnlyList<MemberPickerRow>> GetActiveMembersAsync(CancellationToken cancellationToken = default);
}

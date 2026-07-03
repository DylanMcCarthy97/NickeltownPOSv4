using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Migration;

public interface IMemberMigrationRepository
{
    Task ImportMembersAsync(IReadOnlyList<LegacyMemberDto> members, CancellationToken cancellationToken = default);
}

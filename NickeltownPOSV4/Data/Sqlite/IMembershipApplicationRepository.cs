using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Data.Sqlite;

public interface IMembershipApplicationRepository
{
    Task<IReadOnlyList<MembershipApplicationListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<MembershipApplication?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipApplicationVehicle>> GetVehiclesAsync(long applicationId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(ApplicationStatus status, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(MembershipApplication application, CancellationToken cancellationToken = default);

    Task UpdateAsync(MembershipApplication application, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task ReplaceVehiclesAsync(long applicationId, IReadOnlyList<MembershipApplicationVehicle> vehicles, CancellationToken cancellationToken = default);

    Task<string> GenerateNextApplicationNumberAsync(CancellationToken cancellationToken = default);

    Task<string> GenerateNextMembershipNumberAsync(CancellationToken cancellationToken = default);

    Task<string> GenerateNextReceiptNumberAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipApplicationNote>> GetNotesAsync(long applicationId, CancellationToken cancellationToken = default);

    Task<long> InsertNoteAsync(MembershipApplicationNote note, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipApplicationTimelineEvent>> GetTimelineEventsAsync(long applicationId, CancellationToken cancellationToken = default);

    Task<long> InsertTimelineEventAsync(MembershipApplicationTimelineEvent timelineEvent, CancellationToken cancellationToken = default);
}

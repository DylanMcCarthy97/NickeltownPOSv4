using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public sealed class MembershipFormContentService : IMembershipFormContentService
{
    private readonly IMembershipFormContentRepository _repository;

    public MembershipFormContentService(IMembershipFormContentRepository repository) =>
        _repository = repository;

    public Task<IReadOnlyList<MembershipFormContentSection>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<MembershipFormContentSection?> GetByKeyAsync(string sectionKey, CancellationToken cancellationToken = default) =>
        _repository.GetByKeyAsync(sectionKey, cancellationToken);

    public async Task<string?> GetBodyAsync(string sectionKey, CancellationToken cancellationToken = default)
    {
        var section = await _repository.GetByKeyAsync(sectionKey, cancellationToken).ConfigureAwait(false);
        return section?.Body;
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public sealed class MembershipApplicationValidationResult
{
    public bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}

public interface IMembershipApplicationService
{
    Task<IReadOnlyList<MembershipApplicationListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<MembershipApplicationDetail?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<MembershipApplicationDetail> CreateNewPaperApplicationAsync(CancellationToken cancellationToken = default);

    Task<MembershipApplicationDetail> SaveDraftAsync(MembershipApplicationDetail detail, CancellationToken cancellationToken = default);

    Task<MembershipApplicationDetail> SubmitForReviewAsync(MembershipApplicationDetail detail, CancellationToken cancellationToken = default);

    Task DeleteApplicationAsync(long id, CancellationToken cancellationToken = default);

    MembershipApplicationValidationResult ValidateForSubmit(MembershipApplicationDetail detail);

    Task<MembershipFeeDisplay> GetFeeDisplayAsync(CancellationToken cancellationToken = default);
}

public sealed class MembershipFeeDisplay
{
    public string FullYearLine { get; init; } = string.Empty;

    public string HalfYearLine { get; init; } = string.Empty;

    public string ApplicableFeeLine { get; init; } = string.Empty;

    public decimal FullYearAmount { get; init; }

    public decimal HalfYearAmount { get; init; }

    public decimal ApplicableAmount { get; init; }

    public MembershipFeeType ApplicableFeeType { get; init; }
}

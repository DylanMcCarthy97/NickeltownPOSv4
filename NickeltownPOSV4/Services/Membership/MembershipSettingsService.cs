using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Membership;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Membership;

public sealed class MembershipSettingsService : IMembershipSettingsService
{
    private readonly IMembershipSettingsRepository _settings;
    private readonly IMembershipFormContentRepository _formContent;
    private readonly IAuditLogService _audit;

    public MembershipSettingsService(
        IMembershipSettingsRepository settings,
        IMembershipFormContentRepository formContent,
        IAuditLogService audit)
    {
        _settings = settings;
        _formContent = formContent;
        _audit = audit;
    }

    public Task<MembershipSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        _settings.GetAsync(cancellationToken);

    public async Task SaveAsync(MembershipSettings settings, CancellationToken cancellationToken = default)
    {
        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        await _audit.LogAsync(
            AuditActions.MembershipSettingsUpdated,
            entityType: AuditEntityTypes.MembershipSettings,
            entityId: MembershipSettings.SingletonId.ToString(CultureInfo.InvariantCulture),
            reason: "Membership settings saved").ConfigureAwait(false);
    }

    public async Task<string> FormatFeeStructureLineAsync(
        string sectionKey,
        decimal feeAmount,
        CancellationToken cancellationToken = default)
    {
        var section = await _formContent.GetByKeyAsync(sectionKey, cancellationToken).ConfigureAwait(false);
        var label = section?.Body?.Trim() ?? sectionKey;
        return $"{label} - {feeAmount.ToString("C2", CultureInfo.GetCultureInfo("en-AU"))} joining fee";
    }
}

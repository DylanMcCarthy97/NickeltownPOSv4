using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteMembershipMemberRepository : IMembershipMemberRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteMembershipMemberRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<MembershipMemberListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<MembershipMemberListDbRow>(
            new CommandDefinition(
                """
                SELECT
                  m.Id,
                  m.MemberNumber,
                  m.Surname,
                  m.GivenNames,
                  m.Email,
                  m.Phone,
                  m.MembershipExpiresAt,
                  m.IsActive,
                  COALESCE(a.Status, 'Draft') AS ApplicationStatus
                FROM MembershipMembers m
                LEFT JOIN MembershipApplications a ON a.Id = m.ApplicationId
                ORDER BY m.IsActive DESC, m.Surname, m.GivenNames, m.Id
                """,
                cancellationToken: cancellationToken));

        return Task.FromResult<IReadOnlyList<MembershipMemberListItem>>(rows.Select(MapListRow).ToList());
    }

    public Task<MembershipMember?> GetByApplicationIdAsync(long applicationId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var row = conn.QuerySingleOrDefault<MembershipMemberDbRow>(
            new CommandDefinition(
                """
                SELECT
                  Id, ApplicationId, PosMemberId, MemberNumber,
                  Surname, GivenNames, Email, Phone, Mobile, Address, PostCode, DateOfBirth,
                  MembershipYearLabel, MembershipStartsAt, MembershipExpiresAt, IsActive,
                  ReceiptIssuedAt, AddedToDistributionList, CardIssued, WelcomeBagIssued,
                  CreatedAt, UpdatedAt
                FROM MembershipMembers
                WHERE ApplicationId = @applicationId
                """,
                new { applicationId },
                cancellationToken: cancellationToken));

        return Task.FromResult(row is null ? null : MapRow(row));
    }

    public Task<bool> ExistsForApplicationAsync(long applicationId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM MembershipMembers WHERE ApplicationId = @applicationId",
                new { applicationId },
                cancellationToken: cancellationToken));
        return Task.FromResult(count > 0);
    }

    public Task UpsertFromApplicationAsync(
        MembershipApplication application,
        string membershipYearLabel,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var existing = conn.QuerySingleOrDefault<long?>(
            new CommandDefinition(
                "SELECT Id FROM MembershipMembers WHERE ApplicationId = @applicationId",
                new { applicationId = application.Id },
                cancellationToken: cancellationToken));

        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var parameters = new
        {
            applicationId = application.Id,
            memberNumber = application.MembershipNumber,
            surname = application.Surname,
            givenNames = application.GivenNames,
            email = application.Email,
            phone = application.Phone,
            mobile = application.Mobile,
            address = application.Address,
            postCode = application.PostCode,
            dateOfBirth = FormatDateOnly(application.DateOfBirth),
            membershipYearLabel,
            membershipStartsAt = FormatDateOnly(application.MembershipStart),
            membershipExpiresAt = FormatDateOnly(application.MembershipExpiry),
            isActive = application.Status == ApplicationStatus.MembershipActive ? 1 : 0,
            receiptIssuedAt = FormatOffset(application.ReceiptDate.HasValue
                ? new DateTimeOffset(application.ReceiptDate.Value.ToDateTime(TimeOnly.MinValue))
                : null),
            addedToDistributionList = application.AddedToDistributionList ? 1 : 0,
            cardIssued = application.MembershipCardIssued ? 1 : 0,
            welcomeBagIssued = application.WelcomeBagIssued ? 1 : 0,
            createdAt = now,
            updatedAt = now,
        };

        if (existing is long id)
        {
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE MembershipMembers SET
                      MemberNumber = @memberNumber,
                      Surname = @surname,
                      GivenNames = @givenNames,
                      Email = @email,
                      Phone = @phone,
                      Mobile = @mobile,
                      Address = @address,
                      PostCode = @postCode,
                      DateOfBirth = @dateOfBirth,
                      MembershipYearLabel = @membershipYearLabel,
                      MembershipStartsAt = @membershipStartsAt,
                      MembershipExpiresAt = @membershipExpiresAt,
                      IsActive = @isActive,
                      ReceiptIssuedAt = @receiptIssuedAt,
                      AddedToDistributionList = @addedToDistributionList,
                      CardIssued = @cardIssued,
                      WelcomeBagIssued = @welcomeBagIssued,
                      UpdatedAt = @updatedAt
                    WHERE Id = @id
                    """,
                    new
                    {
                        id,
                        parameters.memberNumber,
                        parameters.surname,
                        parameters.givenNames,
                        parameters.email,
                        parameters.phone,
                        parameters.mobile,
                        parameters.address,
                        parameters.postCode,
                        parameters.dateOfBirth,
                        parameters.membershipYearLabel,
                        parameters.membershipStartsAt,
                        parameters.membershipExpiresAt,
                        parameters.isActive,
                        parameters.receiptIssuedAt,
                        parameters.addedToDistributionList,
                        parameters.cardIssued,
                        parameters.welcomeBagIssued,
                        parameters.updatedAt,
                    },
                    cancellationToken: cancellationToken));
        }
        else
        {
            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO MembershipMembers (
                      ApplicationId, MemberNumber, Surname, GivenNames, Email, Phone, Mobile,
                      Address, PostCode, DateOfBirth, MembershipYearLabel, MembershipStartsAt,
                      MembershipExpiresAt, IsActive, ReceiptIssuedAt, AddedToDistributionList,
                      CardIssued, WelcomeBagIssued, CreatedAt, UpdatedAt
                    ) VALUES (
                      @applicationId, @memberNumber, @surname, @givenNames, @email, @phone, @mobile,
                      @address, @postCode, @dateOfBirth, @membershipYearLabel, @membershipStartsAt,
                      @membershipExpiresAt, @isActive, @receiptIssuedAt, @addedToDistributionList,
                      @cardIssued, @welcomeBagIssued, @createdAt, @updatedAt
                    )
                    """,
                    parameters,
                    cancellationToken: cancellationToken));
        }

        return Task.CompletedTask;
    }

    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM MembershipMembers WHERE IsActive = 1",
                cancellationToken: cancellationToken));
        return Task.FromResult(count);
    }

    public Task<int> CountExpiringWithinDaysAsync(int days, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM MembershipMembers
                WHERE IsActive = 1
                  AND MembershipExpiresAt IS NOT NULL
                  AND date(MembershipExpiresAt) <= date('now', '+' || @days || ' days')
                  AND date(MembershipExpiresAt) >= date('now')
                """,
                new { days },
                cancellationToken: cancellationToken));
        return Task.FromResult(count);
    }

    private static MembershipMemberListItem MapListRow(MembershipMemberListDbRow row)
    {
        var surname = row.Surname?.Trim() ?? string.Empty;
        var givenNames = row.GivenNames?.Trim() ?? string.Empty;
        var memberName = string.Join(" ", new[] { givenNames, surname }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new MembershipMemberListItem
        {
            Id = row.Id,
            MemberNumber = row.MemberNumber,
            MemberName = string.IsNullOrWhiteSpace(memberName) ? row.MemberNumber ?? $"Member #{row.Id}" : memberName,
            ApplicationStatus = Enum.TryParse<ApplicationStatus>(row.ApplicationStatus, out var status)
                ? status
                : ApplicationStatus.Draft,
            MembershipExpiresAt = ParseDateOnly(row.MembershipExpiresAt),
            IsActive = row.IsActive != 0,
            Phone = row.Phone,
            Email = row.Email,
        };
    }

    private static MembershipMember MapRow(MembershipMemberDbRow row) =>
        new()
        {
            Id = row.Id,
            ApplicationId = row.ApplicationId,
            PosMemberId = row.PosMemberId,
            MemberNumber = row.MemberNumber,
            Surname = row.Surname,
            GivenNames = row.GivenNames,
            Email = row.Email,
            Phone = row.Phone,
            Mobile = row.Mobile,
            Address = row.Address,
            PostCode = row.PostCode,
            DateOfBirth = ParseDateOnly(row.DateOfBirth),
            MembershipYearLabel = row.MembershipYearLabel,
            MembershipStartsAt = ParseDateOnly(row.MembershipStartsAt),
            MembershipExpiresAt = ParseDateOnly(row.MembershipExpiresAt),
            IsActive = row.IsActive != 0,
            ReceiptIssuedAt = ParseOffsetNullable(row.ReceiptIssuedAt),
            AddedToDistributionList = row.AddedToDistributionList != 0,
            CardIssued = row.CardIssued != 0,
            WelcomeBagIssued = row.WelcomeBagIssued != 0,
            CreatedAt = ParseOffset(row.CreatedAt),
            UpdatedAt = ParseOffset(row.UpdatedAt),
        };

    private static string? FormatDateOnly(DateOnly? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture);

    private static string? FormatOffset(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateOnly? ParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? DateOnly.FromDateTime(dt)
            : null;
    }

    private static DateTimeOffset ParseOffset(string? value) =>
        string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? DateTimeOffset.UtcNow
            : dto;

    private static DateTimeOffset? ParseOffsetNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? null
            : dto;

    private sealed class MembershipMemberListDbRow
    {
        public long Id { get; init; }

        public string? MemberNumber { get; init; }

        public string? Surname { get; init; }

        public string? GivenNames { get; init; }

        public string? Email { get; init; }

        public string? Phone { get; init; }

        public string? MembershipExpiresAt { get; init; }

        public int IsActive { get; init; }

        public string ApplicationStatus { get; init; } = string.Empty;
    }

    private sealed class MembershipMemberDbRow
    {
        public long Id { get; init; }

        public long? ApplicationId { get; init; }

        public long? PosMemberId { get; init; }

        public string? MemberNumber { get; init; }

        public string? Surname { get; init; }

        public string? GivenNames { get; init; }

        public string? Email { get; init; }

        public string? Phone { get; init; }

        public string? Mobile { get; init; }

        public string? Address { get; init; }

        public string? PostCode { get; init; }

        public string? DateOfBirth { get; init; }

        public string? MembershipYearLabel { get; init; }

        public string? MembershipStartsAt { get; init; }

        public string? MembershipExpiresAt { get; init; }

        public int IsActive { get; init; }

        public string? ReceiptIssuedAt { get; init; }

        public int AddedToDistributionList { get; init; }

        public int CardIssued { get; init; }

        public int WelcomeBagIssued { get; init; }

        public string CreatedAt { get; init; } = string.Empty;

        public string UpdatedAt { get; init; } = string.Empty;
    }
}

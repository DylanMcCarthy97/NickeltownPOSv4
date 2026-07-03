using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteMembershipSettingsRepository : IMembershipSettingsRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteMembershipSettingsRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<MembershipSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var row = conn.QueryFirstOrDefault<MembershipSettingsDbRow>(
            new CommandDefinition(
                """
                SELECT Id, MembershipYearLabel, MembershipYearStart, MembershipYearEnd,
                       JoiningFeeFull, JoiningFeeHalf, RenewalFee, ReminderDaysBeforeExpiry,
                       CommitteeEmail, ClubName, ClubAbn, ClubPoBox, ClubPhone, ClubEmail,
                       LogoPath, UpdatedAt
                FROM MembershipSettings
                WHERE Id = @id
                """,
                new { id = MembershipSettings.SingletonId },
                cancellationToken: cancellationToken));

        return Task.FromResult(row is null ? new MembershipSettings() : MapRow(row));
    }

    public Task SaveAsync(MembershipSettings settings, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO MembershipSettings (
                  Id, MembershipYearLabel, MembershipYearStart, MembershipYearEnd,
                  JoiningFeeFull, JoiningFeeHalf, RenewalFee, ReminderDaysBeforeExpiry,
                  CommitteeEmail, ClubName, ClubAbn, ClubPoBox, ClubPhone, ClubEmail,
                  LogoPath, UpdatedAt)
                VALUES (
                  @Id, @MembershipYearLabel, @MembershipYearStart, @MembershipYearEnd,
                  @JoiningFeeFull, @JoiningFeeHalf, @RenewalFee, @ReminderDaysBeforeExpiry,
                  @CommitteeEmail, @ClubName, @ClubAbn, @ClubPoBox, @ClubPhone, @ClubEmail,
                  @LogoPath, datetime('now'))
                ON CONFLICT(Id) DO UPDATE SET
                  MembershipYearLabel = excluded.MembershipYearLabel,
                  MembershipYearStart = excluded.MembershipYearStart,
                  MembershipYearEnd = excluded.MembershipYearEnd,
                  JoiningFeeFull = excluded.JoiningFeeFull,
                  JoiningFeeHalf = excluded.JoiningFeeHalf,
                  RenewalFee = excluded.RenewalFee,
                  ReminderDaysBeforeExpiry = excluded.ReminderDaysBeforeExpiry,
                  CommitteeEmail = excluded.CommitteeEmail,
                  ClubName = excluded.ClubName,
                  ClubAbn = excluded.ClubAbn,
                  ClubPoBox = excluded.ClubPoBox,
                  ClubPhone = excluded.ClubPhone,
                  ClubEmail = excluded.ClubEmail,
                  LogoPath = excluded.LogoPath,
                  UpdatedAt = datetime('now')
                """,
                new
                {
                    Id = MembershipSettings.SingletonId,
                    settings.MembershipYearLabel,
                    MembershipYearStart = settings.MembershipYearStart.ToString("O", CultureInfo.InvariantCulture),
                    MembershipYearEnd = settings.MembershipYearEnd.ToString("O", CultureInfo.InvariantCulture),
                    settings.JoiningFeeFull,
                    settings.JoiningFeeHalf,
                    settings.RenewalFee,
                    settings.ReminderDaysBeforeExpiry,
                    settings.CommitteeEmail,
                    settings.ClubName,
                    settings.ClubAbn,
                    settings.ClubPoBox,
                    settings.ClubPhone,
                    settings.ClubEmail,
                    settings.LogoPath,
                },
                cancellationToken: cancellationToken));

        return Task.CompletedTask;
    }

    private static MembershipSettings MapRow(MembershipSettingsDbRow row) =>
        new()
        {
            Id = row.Id,
            MembershipYearLabel = row.MembershipYearLabel ?? "2026/2027",
            MembershipYearStart = ParseDateOnly(row.MembershipYearStart, new DateOnly(2026, 7, 1)),
            MembershipYearEnd = ParseDateOnly(row.MembershipYearEnd, new DateOnly(2027, 6, 30)),
            JoiningFeeFull = row.JoiningFeeFull,
            JoiningFeeHalf = row.JoiningFeeHalf,
            RenewalFee = row.RenewalFee,
            ReminderDaysBeforeExpiry = row.ReminderDaysBeforeExpiry,
            CommitteeEmail = row.CommitteeEmail ?? string.Empty,
            ClubName = row.ClubName ?? string.Empty,
            ClubAbn = row.ClubAbn ?? string.Empty,
            ClubPoBox = row.ClubPoBox ?? string.Empty,
            ClubPhone = row.ClubPhone ?? string.Empty,
            ClubEmail = row.ClubEmail ?? string.Empty,
            LogoPath = row.LogoPath,
            UpdatedAt = ParseOffset(row.UpdatedAt),
        };

    private static DateOnly ParseDateOnly(string? value, DateOnly fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            return DateOnly.FromDateTime(dt);
        }

        return fallback;
    }

    private static DateTimeOffset ParseOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.UtcNow;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto
            : DateTimeOffset.UtcNow;
    }

    private sealed class MembershipSettingsDbRow
    {
        public int Id { get; init; }

        public string? MembershipYearLabel { get; init; }

        public string? MembershipYearStart { get; init; }

        public string? MembershipYearEnd { get; init; }

        public decimal JoiningFeeFull { get; init; }

        public decimal JoiningFeeHalf { get; init; }

        public decimal RenewalFee { get; init; }

        public int ReminderDaysBeforeExpiry { get; init; }

        public string? CommitteeEmail { get; init; }

        public string? ClubName { get; init; }

        public string? ClubAbn { get; init; }

        public string? ClubPoBox { get; init; }

        public string? ClubPhone { get; init; }

        public string? ClubEmail { get; init; }

        public string? LogoPath { get; init; }

        public string? UpdatedAt { get; init; }
    }
}

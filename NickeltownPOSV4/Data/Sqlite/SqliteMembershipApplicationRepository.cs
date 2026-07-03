using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteMembershipApplicationRepository : IMembershipApplicationRepository
{
    private readonly SqliteConnectionFactory _factory;

    private const string ApplicationSelectColumns = """
        Id, ApplicationNumber, Source, Status,
        Surname, GivenNames, ChildrenUnder18, Address, PostCode, DateOfBirth,
        Email, Phone, Mobile, AdditionalComments,
        PaperDeclarationSigned, SelectedFee, FeeType,
        ReceiptIssued, ReceiptDate, MembershipAcceptedDate,
        AddedToDistributionList, AddedToMemberRegister, AddedToEmailDistributionList, AddedToSmsDistributionList,
        MembershipCardIssued, WelcomeBagIssued, HasNoVehicle,
        PaymentStatus, PaymentMethod, ReceiptNumber, PaymentEnteredBy, PaymentNotes,
        ApprovedBy, ApprovalDate, MembershipStart, MembershipExpiry, MembershipNumber,
        CreatedBy, SignatureData, SignedAt,
        SubmittedAt, ReviewedAt, ApprovedAt, RejectedAt, RejectionReason,
        CreatedAt, UpdatedAt
        """;

    public SqliteMembershipApplicationRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<MembershipApplicationListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<MembershipApplicationListDbRow>(
            new CommandDefinition(
                $"""
                SELECT
                  a.Id, a.ApplicationNumber, a.Source, a.Status,
                  a.Surname, a.GivenNames, a.SubmittedAt, a.CreatedAt,
                  a.Phone, a.Email,
                  (
                    SELECT RegistrationNumber
                    FROM MembershipApplicationVehicles
                    WHERE ApplicationId = a.Id
                    ORDER BY SortOrder, Id
                    LIMIT 1
                  ) AS PrimaryVehicleRegistration
                FROM MembershipApplications a
                ORDER BY datetime(COALESCE(a.SubmittedAt, a.CreatedAt)) DESC, a.Id DESC
                """,
                cancellationToken: cancellationToken));

        return Task.FromResult<IReadOnlyList<MembershipApplicationListItem>>(rows.Select(MapListRow).ToList());
    }

    public Task<MembershipApplication?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var row = conn.QuerySingleOrDefault<MembershipApplicationDbRow>(
            new CommandDefinition(
                $"SELECT {ApplicationSelectColumns} FROM MembershipApplications WHERE Id = @id",
                new { id },
                cancellationToken: cancellationToken));

        return Task.FromResult(row is null ? null : MapRow(row));
    }

    public Task<IReadOnlyList<MembershipApplicationVehicle>> GetVehiclesAsync(long applicationId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<MembershipApplicationVehicleDbRow>(
            new CommandDefinition(
                """
                SELECT Id, ApplicationId, MakeModel, Year, BodyType, Engine,
                       RegistrationNumber, ClubRego, Colour, Modifications, SortOrder
                FROM MembershipApplicationVehicles
                WHERE ApplicationId = @applicationId
                ORDER BY SortOrder, Id
                """,
                new { applicationId },
                cancellationToken: cancellationToken));

        return Task.FromResult<IReadOnlyList<MembershipApplicationVehicle>>(rows.Select(MapVehicleRow).ToList());
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition("SELECT COUNT(*) FROM MembershipApplications", cancellationToken: cancellationToken));
        return Task.FromResult(count);
    }

    public Task<int> CountByStatusAsync(ApplicationStatus status, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM MembershipApplications WHERE Status = @status",
                new { status = status.ToString() },
                cancellationToken: cancellationToken));
        return Task.FromResult(count);
    }

    public Task<long> InsertAsync(MembershipApplication application, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var id = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                INSERT INTO MembershipApplications (
                  ApplicationNumber, Source, Status,
                  Surname, GivenNames, ChildrenUnder18, Address, PostCode, DateOfBirth,
                  Email, Phone, Mobile, AdditionalComments,
                  PaperDeclarationSigned, SelectedFee, FeeType,
                  ReceiptIssued, ReceiptDate, MembershipAcceptedDate,
                  AddedToDistributionList, AddedToMemberRegister, AddedToEmailDistributionList, AddedToSmsDistributionList,
                  MembershipCardIssued, WelcomeBagIssued, HasNoVehicle,
                  PaymentStatus, PaymentMethod, ReceiptNumber, PaymentEnteredBy, PaymentNotes,
                  ApprovedBy, ApprovalDate, MembershipStart, MembershipExpiry, MembershipNumber,
                  CreatedBy, SignatureData, SignedAt,
                  SubmittedAt, ReviewedAt, ApprovedAt, RejectedAt, RejectionReason,
                  CreatedAt, UpdatedAt)
                VALUES (
                  @ApplicationNumber, @Source, @Status,
                  @Surname, @GivenNames, @ChildrenUnder18, @Address, @PostCode, @DateOfBirth,
                  @Email, @Phone, @Mobile, @AdditionalComments,
                  @PaperDeclarationSigned, @SelectedFee, @FeeType,
                  @ReceiptIssued, @ReceiptDate, @MembershipAcceptedDate,
                  @AddedToDistributionList, @AddedToMemberRegister, @AddedToEmailDistributionList, @AddedToSmsDistributionList,
                  @MembershipCardIssued, @WelcomeBagIssued, @HasNoVehicle,
                  @PaymentStatus, @PaymentMethod, @ReceiptNumber, @PaymentEnteredBy, @PaymentNotes,
                  @ApprovedBy, @ApprovalDate, @MembershipStart, @MembershipExpiry, @MembershipNumber,
                  @CreatedBy, @SignatureData, @SignedAt,
                  @SubmittedAt, @ReviewedAt, @ApprovedAt, @RejectedAt, @RejectionReason,
                  datetime('now'), datetime('now'));
                SELECT last_insert_rowid();
                """,
                MapParameters(application),
                cancellationToken: cancellationToken));

        return Task.FromResult(id);
    }

    public Task UpdateAsync(MembershipApplication application, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(
            new CommandDefinition(
                """
                UPDATE MembershipApplications SET
                  ApplicationNumber = @ApplicationNumber,
                  Source = @Source,
                  Status = @Status,
                  Surname = @Surname,
                  GivenNames = @GivenNames,
                  ChildrenUnder18 = @ChildrenUnder18,
                  Address = @Address,
                  PostCode = @PostCode,
                  DateOfBirth = @DateOfBirth,
                  Email = @Email,
                  Phone = @Phone,
                  Mobile = @Mobile,
                  AdditionalComments = @AdditionalComments,
                  PaperDeclarationSigned = @PaperDeclarationSigned,
                  SelectedFee = @SelectedFee,
                  FeeType = @FeeType,
                  ReceiptIssued = @ReceiptIssued,
                  ReceiptDate = @ReceiptDate,
                  MembershipAcceptedDate = @MembershipAcceptedDate,
                  AddedToDistributionList = @AddedToDistributionList,
                  AddedToMemberRegister = @AddedToMemberRegister,
                  AddedToEmailDistributionList = @AddedToEmailDistributionList,
                  AddedToSmsDistributionList = @AddedToSmsDistributionList,
                  MembershipCardIssued = @MembershipCardIssued,
                  WelcomeBagIssued = @WelcomeBagIssued,
                  HasNoVehicle = @HasNoVehicle,
                  PaymentStatus = @PaymentStatus,
                  PaymentMethod = @PaymentMethod,
                  ReceiptNumber = @ReceiptNumber,
                  PaymentEnteredBy = @PaymentEnteredBy,
                  PaymentNotes = @PaymentNotes,
                  ApprovedBy = @ApprovedBy,
                  ApprovalDate = @ApprovalDate,
                  MembershipStart = @MembershipStart,
                  MembershipExpiry = @MembershipExpiry,
                  MembershipNumber = @MembershipNumber,
                  CreatedBy = @CreatedBy,
                  SignatureData = @SignatureData,
                  SignedAt = @SignedAt,
                  SubmittedAt = @SubmittedAt,
                  ReviewedAt = @ReviewedAt,
                  ApprovedAt = @ApprovedAt,
                  RejectedAt = @RejectedAt,
                  RejectionReason = @RejectionReason,
                  UpdatedAt = datetime('now')
                WHERE Id = @Id
                """,
                MapParameters(application, includeId: true),
                cancellationToken: cancellationToken));

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var affected = conn.Execute(
            new CommandDefinition(
                "DELETE FROM MembershipApplications WHERE Id = @id AND Status = @status",
                new { id, status = ApplicationStatus.Draft.ToString() },
                cancellationToken: cancellationToken));
        return Task.FromResult(affected > 0);
    }

    public Task ReplaceVehiclesAsync(long applicationId, IReadOnlyList<MembershipApplicationVehicle> vehicles, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        conn.Execute(
            new CommandDefinition(
                "DELETE FROM MembershipApplicationVehicles WHERE ApplicationId = @applicationId",
                new { applicationId },
                transaction: tx,
                cancellationToken: cancellationToken));

        var sortOrder = 0;
        foreach (var vehicle in vehicles)
        {
            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO MembershipApplicationVehicles (
                      ApplicationId, MakeModel, Year, BodyType, Engine,
                      RegistrationNumber, ClubRego, Colour, Modifications, SortOrder)
                    VALUES (
                      @ApplicationId, @MakeModel, @Year, @BodyType, @Engine,
                      @RegistrationNumber, @ClubRego, @Colour, @Modifications, @SortOrder)
                    """,
                    new
                    {
                        ApplicationId = applicationId,
                        vehicle.MakeModel,
                        vehicle.Year,
                        vehicle.BodyType,
                        vehicle.Engine,
                        vehicle.RegistrationNumber,
                        vehicle.ClubRego,
                        vehicle.Colour,
                        vehicle.Modifications,
                        SortOrder = sortOrder++,
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task<string> GenerateNextApplicationNumberAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var year = DateTime.UtcNow.Year;
        var prefix = $"APP-{year}-";
        var max = conn.ExecuteScalar<string?>(
            new CommandDefinition(
                """
                SELECT ApplicationNumber
                FROM MembershipApplications
                WHERE ApplicationNumber LIKE @pattern
                ORDER BY ApplicationNumber DESC
                LIMIT 1
                """,
                new { pattern = prefix + "%" },
                cancellationToken: cancellationToken));

        var next = 1;
        if (!string.IsNullOrWhiteSpace(max) && max.Length > prefix.Length)
        {
            var suffix = max[prefix.Length..];
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                next = parsed + 1;
            }
        }

        return Task.FromResult($"{prefix}{next:D4}");
    }

    public Task<string> GenerateNextMembershipNumberAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var year = DateTime.UtcNow.Year;
        var prefix = $"NTF-{year}-";
        var maxFromApps = conn.ExecuteScalar<string?>(
            new CommandDefinition(
                """
                SELECT MembershipNumber
                FROM MembershipApplications
                WHERE MembershipNumber LIKE @pattern
                ORDER BY MembershipNumber DESC
                LIMIT 1
                """,
                new { pattern = prefix + "%" },
                cancellationToken: cancellationToken));
        var maxFromMembers = conn.ExecuteScalar<string?>(
            new CommandDefinition(
                """
                SELECT MemberNumber
                FROM MembershipMembers
                WHERE MemberNumber LIKE @pattern
                ORDER BY MemberNumber DESC
                LIMIT 1
                """,
                new { pattern = prefix + "%" },
                cancellationToken: cancellationToken));

        var next = 1;
        foreach (var max in new[] { maxFromApps, maxFromMembers })
        {
            if (string.IsNullOrWhiteSpace(max) || max.Length <= prefix.Length)
            {
                continue;
            }

            var suffix = max[prefix.Length..];
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                next = Math.Max(next, parsed + 1);
            }
        }

        return Task.FromResult($"{prefix}{next:D4}");
    }

    public Task<string> GenerateNextReceiptNumberAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var year = DateTime.UtcNow.Year;
        var prefix = $"RCP-{year}-";
        var max = conn.ExecuteScalar<string?>(
            new CommandDefinition(
                """
                SELECT ReceiptNumber
                FROM MembershipApplications
                WHERE ReceiptNumber LIKE @pattern
                ORDER BY ReceiptNumber DESC
                LIMIT 1
                """,
                new { pattern = prefix + "%" },
                cancellationToken: cancellationToken));

        var next = 1;
        if (!string.IsNullOrWhiteSpace(max) && max.Length > prefix.Length)
        {
            var suffix = max[prefix.Length..];
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                next = parsed + 1;
            }
        }

        return Task.FromResult($"{prefix}{next:D4}");
    }

    public Task<IReadOnlyList<MembershipApplicationNote>> GetNotesAsync(long applicationId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<MembershipApplicationNoteDbRow>(
            new CommandDefinition(
                """
                SELECT Id, ApplicationId, Author, Text, CreatedAt
                FROM MembershipApplicationNotes
                WHERE ApplicationId = @applicationId
                ORDER BY datetime(CreatedAt) DESC, Id DESC
                """,
                new { applicationId },
                cancellationToken: cancellationToken));

        return Task.FromResult<IReadOnlyList<MembershipApplicationNote>>(rows.Select(MapNoteRow).ToList());
    }

    public Task<long> InsertNoteAsync(MembershipApplicationNote note, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var id = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                INSERT INTO MembershipApplicationNotes (ApplicationId, Author, Text, CreatedAt)
                VALUES (@ApplicationId, @Author, @Text, @CreatedAt);
                SELECT last_insert_rowid();
                """,
                new
                {
                    note.ApplicationId,
                    note.Author,
                    note.Text,
                    CreatedAt = FormatOffset(note.CreatedAt),
                },
                cancellationToken: cancellationToken));

        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<MembershipApplicationTimelineEvent>> GetTimelineEventsAsync(long applicationId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<MembershipApplicationTimelineEventDbRow>(
            new CommandDefinition(
                """
                SELECT Id, ApplicationId, EventType, UserName, Description, OccurredAt
                FROM MembershipApplicationTimelineEvents
                WHERE ApplicationId = @applicationId
                ORDER BY datetime(OccurredAt) DESC, Id DESC
                """,
                new { applicationId },
                cancellationToken: cancellationToken));

        return Task.FromResult<IReadOnlyList<MembershipApplicationTimelineEvent>>(rows.Select(MapTimelineRow).ToList());
    }

    public Task<long> InsertTimelineEventAsync(MembershipApplicationTimelineEvent timelineEvent, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var id = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                INSERT INTO MembershipApplicationTimelineEvents (ApplicationId, EventType, UserName, Description, OccurredAt)
                VALUES (@ApplicationId, @EventType, @UserName, @Description, @OccurredAt);
                SELECT last_insert_rowid();
                """,
                new
                {
                    timelineEvent.ApplicationId,
                    EventType = timelineEvent.EventType.ToString(),
                    UserName = timelineEvent.User,
                    timelineEvent.Description,
                    OccurredAt = FormatOffset(timelineEvent.OccurredAt),
                },
                cancellationToken: cancellationToken));

        return Task.FromResult(id);
    }

    private static MembershipApplicationNote MapNoteRow(MembershipApplicationNoteDbRow row) =>
        new()
        {
            Id = row.Id,
            ApplicationId = row.ApplicationId,
            Author = row.Author,
            Text = row.Text,
            CreatedAt = ParseOffset(row.CreatedAt),
        };

    private static MembershipApplicationTimelineEvent MapTimelineRow(MembershipApplicationTimelineEventDbRow row) =>
        new()
        {
            Id = row.Id,
            ApplicationId = row.ApplicationId,
            EventType = Enum.TryParse<MembershipTimelineEventType>(row.EventType, out var eventType)
                ? eventType
                : MembershipTimelineEventType.Edited,
            User = row.UserName,
            Description = row.Description,
            OccurredAt = ParseOffset(row.OccurredAt),
        };

    private static object MapParameters(MembershipApplication application, bool includeId = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["ApplicationNumber"] = application.ApplicationNumber,
            ["Source"] = application.Source.ToString(),
            ["Status"] = application.Status.ToString(),
            ["Surname"] = application.Surname,
            ["GivenNames"] = application.GivenNames,
            ["ChildrenUnder18"] = application.ChildrenUnder18,
            ["Address"] = application.Address,
            ["PostCode"] = application.PostCode,
            ["DateOfBirth"] = application.DateOfBirth?.ToString("O", CultureInfo.InvariantCulture),
            ["Email"] = application.Email,
            ["Phone"] = application.Phone,
            ["Mobile"] = application.Mobile,
            ["AdditionalComments"] = application.AdditionalComments,
            ["PaperDeclarationSigned"] = application.PaperDeclarationSigned ? 1 : 0,
            ["SelectedFee"] = application.SelectedFee,
            ["FeeType"] = application.FeeType?.ToString(),
            ["ReceiptIssued"] = application.ReceiptIssued ? 1 : 0,
            ["ReceiptDate"] = FormatDateOnly(application.ReceiptDate),
            ["MembershipAcceptedDate"] = FormatDateOnly(application.MembershipAcceptedDate),
            ["AddedToDistributionList"] = application.AddedToDistributionList ? 1 : 0,
            ["AddedToMemberRegister"] = application.AddedToMemberRegister ? 1 : 0,
            ["AddedToEmailDistributionList"] = application.AddedToEmailDistributionList ? 1 : 0,
            ["AddedToSmsDistributionList"] = application.AddedToSmsDistributionList ? 1 : 0,
            ["MembershipCardIssued"] = application.MembershipCardIssued ? 1 : 0,
            ["WelcomeBagIssued"] = application.WelcomeBagIssued ? 1 : 0,
            ["HasNoVehicle"] = application.HasNoVehicle ? 1 : 0,
            ["PaymentStatus"] = application.PaymentStatus.ToString(),
            ["PaymentMethod"] = application.PaymentMethod?.ToString(),
            ["ReceiptNumber"] = application.ReceiptNumber,
            ["PaymentEnteredBy"] = application.PaymentEnteredBy,
            ["PaymentNotes"] = application.PaymentNotes,
            ["ApprovedBy"] = application.ApprovedBy,
            ["ApprovalDate"] = FormatDateOnly(application.ApprovalDate),
            ["MembershipStart"] = FormatDateOnly(application.MembershipStart),
            ["MembershipExpiry"] = FormatDateOnly(application.MembershipExpiry),
            ["MembershipNumber"] = application.MembershipNumber,
            ["CreatedBy"] = application.CreatedBy,
            ["SignatureData"] = application.SignatureData,
            ["SignedAt"] = FormatOffset(application.SignedAt),
            ["SubmittedAt"] = FormatOffset(application.SubmittedAt),
            ["ReviewedAt"] = FormatOffset(application.ReviewedAt),
            ["ApprovedAt"] = FormatOffset(application.ApprovedAt),
            ["RejectedAt"] = FormatOffset(application.RejectedAt),
            ["RejectionReason"] = application.RejectionReason,
        };

        if (includeId)
        {
            parameters["Id"] = application.Id;
        }

        return parameters;
    }

    private static MembershipApplicationListItem MapListRow(MembershipApplicationListDbRow row)
    {
        var name = $"{row.GivenNames} {row.Surname}".Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = row.ApplicationNumber ?? $"Application #{row.Id}";
        }

        return new MembershipApplicationListItem
        {
            Id = row.Id,
            ApplicationNumber = row.ApplicationNumber,
            ApplicantName = name,
            Source = Enum.TryParse<ApplicationSource>(row.Source, out var source) ? source : ApplicationSource.Paper,
            Status = Enum.TryParse<ApplicationStatus>(row.Status, out var status) ? status : ApplicationStatus.Submitted,
            SubmittedAt = ParseOffset(row.SubmittedAt),
            CreatedAt = ParseOffset(row.CreatedAt),
            Phone = row.Phone ?? row.Mobile,
            Email = row.Email,
            PrimaryVehicleRegistration = row.PrimaryVehicleRegistration,
        };
    }

    private static MembershipApplication MapRow(MembershipApplicationDbRow row) =>
        new()
        {
            Id = row.Id,
            ApplicationNumber = row.ApplicationNumber,
            Source = Enum.TryParse<ApplicationSource>(row.Source, out var source) ? source : ApplicationSource.Paper,
            Status = Enum.TryParse<ApplicationStatus>(row.Status, out var status) ? status : ApplicationStatus.Submitted,
            Surname = row.Surname,
            GivenNames = row.GivenNames,
            ChildrenUnder18 = row.ChildrenUnder18,
            Address = row.Address,
            PostCode = row.PostCode,
            DateOfBirth = ParseDateOnly(row.DateOfBirth),
            Email = row.Email,
            Phone = row.Phone,
            Mobile = row.Mobile,
            AdditionalComments = row.AdditionalComments,
            PaperDeclarationSigned = row.PaperDeclarationSigned != 0,
            SelectedFee = row.SelectedFee,
            FeeType = Enum.TryParse<MembershipFeeType>(row.FeeType, out var feeType) ? feeType : null,
            ReceiptIssued = row.ReceiptIssued != 0,
            ReceiptDate = ParseDateOnly(row.ReceiptDate),
            MembershipAcceptedDate = ParseDateOnly(row.MembershipAcceptedDate),
            AddedToDistributionList = row.AddedToDistributionList != 0,
            AddedToMemberRegister = row.AddedToMemberRegister != 0,
            AddedToEmailDistributionList = row.AddedToEmailDistributionList != 0,
            AddedToSmsDistributionList = row.AddedToSmsDistributionList != 0,
            MembershipCardIssued = row.MembershipCardIssued != 0,
            WelcomeBagIssued = row.WelcomeBagIssued != 0,
            HasNoVehicle = row.HasNoVehicle != 0,
            PaymentStatus = Enum.TryParse<MembershipPaymentStatus>(row.PaymentStatus, out var paymentStatus)
                ? paymentStatus
                : MembershipPaymentStatus.AwaitingPayment,
            PaymentMethod = Enum.TryParse<MembershipPaymentMethod>(row.PaymentMethod, out var paymentMethod)
                ? paymentMethod
                : null,
            ReceiptNumber = row.ReceiptNumber,
            PaymentEnteredBy = row.PaymentEnteredBy,
            PaymentNotes = row.PaymentNotes,
            ApprovedBy = row.ApprovedBy,
            ApprovalDate = ParseDateOnly(row.ApprovalDate),
            MembershipStart = ParseDateOnly(row.MembershipStart),
            MembershipExpiry = ParseDateOnly(row.MembershipExpiry),
            MembershipNumber = row.MembershipNumber,
            CreatedBy = row.CreatedBy,
            SignatureData = row.SignatureData,
            SignedAt = ParseOffsetNullable(row.SignedAt),
            SubmittedAt = ParseOffset(row.SubmittedAt),
            ReviewedAt = ParseOffsetNullable(row.ReviewedAt),
            ApprovedAt = ParseOffsetNullable(row.ApprovedAt),
            RejectedAt = ParseOffsetNullable(row.RejectedAt),
            RejectionReason = row.RejectionReason,
            CreatedAt = ParseOffset(row.CreatedAt),
            UpdatedAt = ParseOffset(row.UpdatedAt),
        };

    private static MembershipApplicationVehicle MapVehicleRow(MembershipApplicationVehicleDbRow row) =>
        new()
        {
            Id = row.Id,
            ApplicationId = row.ApplicationId,
            MakeModel = row.MakeModel,
            Year = row.Year,
            BodyType = row.BodyType,
            Engine = row.Engine,
            RegistrationNumber = row.RegistrationNumber,
            ClubRego = row.ClubRego,
            Colour = row.Colour,
            Modifications = row.Modifications,
            SortOrder = row.SortOrder,
        };

    private static string? FormatDateOnly(DateOnly? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture);

    private static string? FormatOffset(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string FormatOffset(DateTimeOffset value) =>
        (value == default ? DateTimeOffset.UtcNow : value).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

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

    private sealed class MembershipApplicationListDbRow
    {
        public long Id { get; init; }

        public string? ApplicationNumber { get; init; }

        public string Source { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string? Surname { get; init; }

        public string? GivenNames { get; init; }

        public string SubmittedAt { get; init; } = string.Empty;

        public string CreatedAt { get; init; } = string.Empty;

        public string? Phone { get; init; }

        public string? Mobile { get; init; }

        public string? Email { get; init; }

        public string? PrimaryVehicleRegistration { get; init; }
    }

    private sealed class MembershipApplicationDbRow
    {
        public long Id { get; init; }

        public string? ApplicationNumber { get; init; }

        public string Source { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string? Surname { get; init; }

        public string? GivenNames { get; init; }

        public string? ChildrenUnder18 { get; init; }

        public string? Address { get; init; }

        public string? PostCode { get; init; }

        public string? DateOfBirth { get; init; }

        public string? Email { get; init; }

        public string? Phone { get; init; }

        public string? Mobile { get; init; }

        public string? AdditionalComments { get; init; }

        public int PaperDeclarationSigned { get; init; }

        public decimal? SelectedFee { get; init; }

        public string? FeeType { get; init; }

        public int ReceiptIssued { get; init; }

        public string? ReceiptDate { get; init; }

        public string? MembershipAcceptedDate { get; init; }

        public int AddedToDistributionList { get; init; }

        public int AddedToMemberRegister { get; init; }

        public int AddedToEmailDistributionList { get; init; }

        public int AddedToSmsDistributionList { get; init; }

        public int MembershipCardIssued { get; init; }

        public int WelcomeBagIssued { get; init; }

        public int HasNoVehicle { get; init; }

        public string PaymentStatus { get; init; } = MembershipPaymentStatus.AwaitingPayment.ToString();

        public string? PaymentMethod { get; init; }

        public string? ReceiptNumber { get; init; }

        public string? PaymentEnteredBy { get; init; }

        public string? PaymentNotes { get; init; }

        public string? ApprovedBy { get; init; }

        public string? ApprovalDate { get; init; }

        public string? MembershipStart { get; init; }

        public string? MembershipExpiry { get; init; }

        public string? MembershipNumber { get; init; }

        public string? CreatedBy { get; init; }

        public string? SignatureData { get; init; }

        public string? SignedAt { get; init; }

        public string SubmittedAt { get; init; } = string.Empty;

        public string? ReviewedAt { get; init; }

        public string? ApprovedAt { get; init; }

        public string? RejectedAt { get; init; }

        public string? RejectionReason { get; init; }

        public string CreatedAt { get; init; } = string.Empty;

        public string UpdatedAt { get; init; } = string.Empty;
    }

    private sealed class MembershipApplicationVehicleDbRow
    {
        public long Id { get; init; }

        public long ApplicationId { get; init; }

        public string? MakeModel { get; init; }

        public string? Year { get; init; }

        public string? BodyType { get; init; }

        public string? Engine { get; init; }

        public string? RegistrationNumber { get; init; }

        public string? ClubRego { get; init; }

        public string? Colour { get; init; }

        public string? Modifications { get; init; }

        public int SortOrder { get; init; }
    }

    private sealed class MembershipApplicationNoteDbRow
    {
        public long Id { get; init; }

        public long ApplicationId { get; init; }

        public string Author { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;

        public string CreatedAt { get; init; } = string.Empty;
    }

    private sealed class MembershipApplicationTimelineEventDbRow
    {
        public long Id { get; init; }

        public long ApplicationId { get; init; }

        public string EventType { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string OccurredAt { get; init; } = string.Empty;
    }
}

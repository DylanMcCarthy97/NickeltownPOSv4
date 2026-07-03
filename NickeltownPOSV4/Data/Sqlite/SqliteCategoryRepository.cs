using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteCategoryRepository : ICategoryMigrationRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteCategoryRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task ImportCategoriesAsync(IReadOnlyList<LegacyCategoryDto> categories, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var dto in categories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpsertCategory(conn, tx, dto, cancellationToken);
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    private static void UpsertCategory(SqliteConnection conn, SqliteTransaction tx, LegacyCategoryDto dto, CancellationToken cancellationToken)
    {
        var slug = FirstNonEmpty(dto.Key, dto.Name) ?? "category";
        var legacyId = string.IsNullOrWhiteSpace(dto.Id) ? LegacyStableId.ForCategory(dto) : dto.Id!.Trim();
        var name = FirstNonEmpty(dto.DisplayName, dto.Name) ?? "Category";
        var sort = dto.SortOrder ?? 0;
        var active = dto.Active != false && dto.IsActive != false ? 1 : 0;
        var legacyKey = slug.Trim();

        var existing = conn.QuerySingleOrDefault<long?>(
            new CommandDefinition(
                """
                SELECT Id FROM Categories
                WHERE (LegacyId IS NOT NULL AND LegacyId = @LegacyId)
                   OR (LegacyKey IS NOT NULL AND (LegacyKey = @LegacyId OR LegacyKey = @LegacyKey))
                   OR lower(trim(Name)) = lower(trim(@Name))
                LIMIT 1
                """,
                new { LegacyId = legacyId, LegacyKey = legacyKey, Name = name },
                tx,
                cancellationToken: cancellationToken));

        if (existing is long pk)
        {
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Categories
                    SET Name = @Name,
                        SortOrder = @SortOrder,
                        IsActive = @Active,
                        LegacyId = COALESCE(LegacyId, @LegacyId),
                        LegacyKey = COALESCE(LegacyKey, @LegacyKey),
                        UpdatedAt = datetime('now')
                    WHERE Id = @Id
                    """,
                    new
                    {
                        Id = pk,
                        Name = name,
                        SortOrder = sort,
                        Active = active,
                        LegacyId = legacyId,
                        LegacyKey = legacyKey,
                    },
                tx,
                cancellationToken: cancellationToken));
        }
        else
        {
            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO Categories (LegacyId, LegacyKey, Name, SortOrder, IsActive, CreatedAt, UpdatedAt)
                    VALUES (@LegacyId, @LegacyKey, @Name, @SortOrder, @Active, datetime('now'), datetime('now'))
                    """,
                    new
                    {
                        LegacyId = legacyId,
                        LegacyKey = legacyKey,
                        Name = name,
                        SortOrder = sort,
                        Active = active,
                    },
                tx,
                cancellationToken: cancellationToken));
        }
    }

    /// <summary>Resolves SQLite <c>Categories.Id</c> for an item/drink import row; may insert a category from the name.</summary>
    public static long ResolveCategoryIdForProduct(
        SqliteConnection conn,
        SqliteTransaction tx,
        string? legacyCategoryId,
        string? categoryName)
    {
        var trimmedId = string.IsNullOrWhiteSpace(legacyCategoryId) ? null : legacyCategoryId.Trim();
        var trimmedName = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();

        if (trimmedId is not null)
        {
            var byLegacy = conn.QuerySingleOrDefault<long?>(
                """
                SELECT Id FROM Categories
                WHERE LegacyId = @k OR LegacyKey = @k OR CAST(Id AS TEXT) = @k
                LIMIT 1
                """,
                new { k = trimmedId },
                tx);
            if (byLegacy is long a)
            {
                return a;
            }
        }

        if (trimmedName is not null)
        {
            var byName = conn.QuerySingleOrDefault<long?>(
                """
                SELECT Id FROM Categories
                WHERE lower(trim(Name)) = lower(trim(@n))
                LIMIT 1
                """,
                new { n = trimmedName },
                tx);
            if (byName is long b)
            {
                return b;
            }

            var autoLegacy = "auto_cn_" + LegacyStableId.HashHex(trimmedName.ToLowerInvariant());
            conn.Execute(
                """
                INSERT INTO Categories (LegacyId, LegacyKey, Name, SortOrder, IsActive, CreatedAt, UpdatedAt)
                VALUES (@LegacyId, @LegacyKey, @Name, 0, 1, datetime('now'), datetime('now'))
                """,
                new { LegacyId = autoLegacy, LegacyKey = autoLegacy, Name = trimmedName },
                tx);
            return conn.QuerySingle<long>("SELECT last_insert_rowid()", transaction: tx);
        }

        return conn.QuerySingle<long>(
            "SELECT Id FROM Categories WHERE LegacyKey = 'default' LIMIT 1",
            transaction: tx);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v.Trim();
            }
        }

        return null;
    }
}

using System;
using System.Globalization;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>
/// Resolves a tab id shown on the workspace board (legacy string or synthetic tab_pk_{rowid}) into SQLite lookup parameters.
/// </summary>
internal static class TabBoardRoute
{
    public const string SqlitePkPrefix = "tab_pk_";

    public static string ForSqlitePrimaryKey(long sqliteTabId) =>
        $"{SqlitePkPrefix}{sqliteTabId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Returns legacy id (non-null) or SQLite primary key (non-null), never both.
    /// </summary>
    public static bool TryParse(string? boardTabId, out string? legacyId, out long? sqliteTabId)
    {
        legacyId = null;
        sqliteTabId = null;
        var t = (boardTabId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(t))
        {
            return false;
        }

        if (t.StartsWith(SqlitePkPrefix, StringComparison.Ordinal)
            && long.TryParse(t.AsSpan(SqlitePkPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pk)
            && pk > 0)
        {
            sqliteTabId = pk;
            return true;
        }

        legacyId = t;
        return true;
    }
}
using System;
using Microsoft.Data.Sqlite;

namespace NickeltownPOSV4.Data.Sqlite;

internal static class SqliteConstraint
{
    public const int Unique = 2067;
    public const int PrimaryKey = 1555;

    public static bool IsUniqueViolation(Exception ex) =>
        ex is SqliteException se
        && (se.SqliteExtendedErrorCode is Unique or PrimaryKey
            || se.SqliteErrorCode == 19);
}

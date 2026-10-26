using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Sqlite;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class SqliteConstraintTests
{
    [Theory]
    [InlineData("INSERT INTO Children VALUES (1, 'second', 1, 1)", true)]
    [InlineData("INSERT INTO Children VALUES (2, 'existing', 1, 1)", true)]
    [InlineData("INSERT INTO Children VALUES (2, 'second', 99, 1)", false)]
    [InlineData("INSERT INTO Children VALUES (2, 'second', 1, -1)", false)]
    public void OnlyUniquenessFailuresCountAsIdempotentDuplicates(string sql, bool unique)
    {
        using var db = new SqliteConnection("Data Source=:memory:");
        db.Open();
        db.Execute("""
            PRAGMA foreign_keys = ON;
            CREATE TABLE Parents (Id INTEGER PRIMARY KEY);
            CREATE TABLE Children (Id INTEGER PRIMARY KEY, Name TEXT UNIQUE, ParentId INTEGER REFERENCES Parents(Id), Qty INTEGER CHECK (Qty >= 0));
            INSERT INTO Parents VALUES (1);
            INSERT INTO Children VALUES (1, 'existing', 1, 1);
            """);
        var error = Assert.Throws<SqliteException>(() => db.Execute(sql));
        Assert.Equal(unique, SqliteConstraint.IsUniqueViolation(error));
    }
}

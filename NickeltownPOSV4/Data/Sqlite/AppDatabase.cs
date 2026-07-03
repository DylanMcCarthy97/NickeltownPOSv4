using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Application SQLite database (V4 persistence).</summary>
public sealed class AppDatabase
{
    private readonly DatabaseInitializer _initializer;

    public AppDatabase(SqliteConnectionFactory factory, DatabaseInitializer initializer)
    {
        DatabaseFilePath = factory.DataSourcePath;
        _initializer = initializer;
    }

    public string DatabaseFilePath { get; }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
        _initializer.InitializeAsync(cancellationToken);
}

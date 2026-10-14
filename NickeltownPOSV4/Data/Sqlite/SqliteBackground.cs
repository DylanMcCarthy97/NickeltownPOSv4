using System;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Runs SQLite work off the UI thread so catalog/tab queries cannot freeze WinUI.</summary>
internal static class SqliteBackground
{
    public static Task<T> Run<T>(Func<T> work, CancellationToken cancellationToken = default) =>
        Task.Run(work, cancellationToken);
}

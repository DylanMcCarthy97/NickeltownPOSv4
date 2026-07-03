using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Reads open tabs from SQLite for the tabs grid.</summary>
public interface ITabWorkspaceQuery
{
    Task<IReadOnlyList<TabCardModel>> GetOpenTabCardsAsync(CancellationToken cancellationToken = default);
}

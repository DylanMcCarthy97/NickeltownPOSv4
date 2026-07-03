using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.Pitstop;

internal static class PitstopCatalogPager
{
    public static int TotalPages(int itemCount, int pageSize) =>
        Math.Max(1, (int)Math.Ceiling(itemCount / (double)pageSize));

    public static int ClampPage(int page, int itemCount, int pageSize)
    {
        var total = TotalPages(itemCount, pageSize);
        if (page > total)
        {
            return total;
        }

        return page < 1 ? 1 : page;
    }

    public static IReadOnlyList<PitstopCatalogProductRow> GetPage(
        IReadOnlyList<PitstopCatalogProductRow> filtered,
        int page,
        int pageSize) =>
        filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
}

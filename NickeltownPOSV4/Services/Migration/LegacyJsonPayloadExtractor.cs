using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Services.Migration;

/// <summary>Locates tabular JSON arrays in WinForms V2 files (root arrays, wrapped arrays, shallow nesting).</summary>
internal static class LegacyJsonPayloadExtractor
{
    private static readonly string[] CommonArrayHints =
    [
        "Payload", "payload", "Body", "body", "Content", "content",
        "Items", "items", "Data", "data", "Records", "records", "Rows", "rows", "Values", "values",
        "Results", "results", "Entries", "entries", "List", "list", "Collection", "collection",
    ];

    private static readonly string[] WrapperObjectHints =
    [
        "Data", "data", "Payload", "payload", "Result", "result", "Content", "content", "Value", "value",
        "Root", "root", "Entity", "entity", "Document", "document", "Body", "body",
    ];

    public static string DescribeJsonValueKind(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Array => "Array",
        JsonValueKind.Object => "Object",
        JsonValueKind.String => "String",
        JsonValueKind.Number => "Number",
        JsonValueKind.True or JsonValueKind.False => "Boolean",
        JsonValueKind.Null => "Null",
        JsonValueKind.Undefined => "Undefined",
        _ => kind.ToString(),
    };

    public static int CountArrayElements(JsonElement arrayElement) =>
        arrayElement.ValueKind == JsonValueKind.Array ? arrayElement.GetArrayLength() : 0;

    /// <summary>Resolves the primary collection used for migration preview/import for this file kind.</summary>
    public static bool TryGetPrimaryDataArray(JsonElement root, LegacyJsonFileKind kind, out JsonElement array, out string matchedPath)
    {
        array = default;
        matchedPath = string.Empty;

        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
            matchedPath = "(root array)";
            return true;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (kind is LegacyJsonFileKind.Items or LegacyJsonFileKind.Drinks or LegacyJsonFileKind.Categories)
        {
            if (TrySelectDominantArrayUnderObject(root, kind, maxObjectDepth: 10, out array, out matchedPath))
            {
                return true;
            }
        }

        if (TryHintsThenLargest(root, kind, string.Empty, out array, out matchedPath))
        {
            return true;
        }

        foreach (var wrap in WrapperObjectHints)
        {
            if (!TryGetPropertyIgnoreCase(root, wrap, out var inner) || inner.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (kind is LegacyJsonFileKind.Items or LegacyJsonFileKind.Drinks or LegacyJsonFileKind.Categories)
            {
                if (TrySelectDominantArrayUnderObject(inner, kind, maxObjectDepth: 8, out array, out var subPath))
                {
                    matchedPath = wrap + "." + subPath;
                    return true;
                }
            }

            if (TryHintsThenLargest(inner, kind, wrap + ".", out array, out var subPath2))
            {
                matchedPath = subPath2;
                return true;
            }
        }

        var childIndex = 0;
        foreach (var prop in root.EnumerateObject().OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (childIndex++ > 48)
            {
                break;
            }

            if (prop.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (kind is LegacyJsonFileKind.Items or LegacyJsonFileKind.Drinks or LegacyJsonFileKind.Categories)
            {
                if (TrySelectDominantArrayUnderObject(prop.Value, kind, maxObjectDepth: 8, out array, out var subPath))
                {
                    matchedPath = prop.Name + "." + subPath;
                    return true;
                }
            }

            if (TryHintsThenLargest(prop.Value, kind, prop.Name + ".", out array, out var subPath2))
            {
                matchedPath = subPath2;
                return true;
            }
        }

        return false;
    }

    /// <summary>For stock-like wrapped documents: pick the largest JSON array under nested objects (tie-break by hint rank).</summary>
    private static bool TrySelectDominantArrayUnderObject(
        JsonElement rootObject,
        LegacyJsonFileKind kind,
        int maxObjectDepth,
        out JsonElement array,
        out string matchedPath)
    {
        array = default;
        matchedPath = string.Empty;
        if (rootObject.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var candidates = new List<(string Path, JsonElement Arr, int Len, int Rank)>();
        CollectArrayCandidates(rootObject, prefix: string.Empty, objectDepth: 0, maxObjectDepth, candidates, kind);

        if (candidates.Count == 0)
        {
            return false;
        }

        var best = candidates
            .OrderByDescending(c => c.Len)
            .ThenBy(c => c.Rank)
            .ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .First();

        array = best.Arr;
        matchedPath = best.Path;
        return true;
    }

    private static void CollectArrayCandidates(
        JsonElement obj,
        string prefix,
        int objectDepth,
        int maxObjectDepth,
        List<(string Path, JsonElement Arr, int Len, int Rank)> candidates,
        LegacyJsonFileKind kind)
    {
        if (obj.ValueKind != JsonValueKind.Object || objectDepth > maxObjectDepth)
        {
            return;
        }

        var safety = 0;
        foreach (var p in obj.EnumerateObject())
        {
            if (++safety > 120)
            {
                break;
            }

            var path = string.IsNullOrEmpty(prefix) ? p.Name : prefix + "." + p.Name;

            if (p.Value.ValueKind == JsonValueKind.Array)
            {
                var len = p.Value.GetArrayLength();
                var rank = PropertyHintRank(p.Name, kind);
                candidates.Add((path, p.Value, len, rank));
            }
            else if (p.Value.ValueKind == JsonValueKind.Object)
            {
                CollectArrayCandidates(p.Value, path, objectDepth + 1, maxObjectDepth, candidates, kind);
            }
        }
    }

    private static int PropertyHintRank(string propertyName, LegacyJsonFileKind kind)
    {
        var i = 0;
        foreach (var h in GetOrderedHints(kind))
        {
            if (string.Equals(propertyName, h, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }

            i++;
        }

        return 10_000 + (Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(propertyName)) % 1000);
    }

    public static bool TryGetPropertyIgnoreCase(JsonElement obj, string candidate, out JsonElement value)
    {
        value = default;
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        return false;
    }

    public static int CountTabHistoryLinesInTabsArray(JsonElement tabsArray)
    {
        if (tabsArray.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var history = 0;
        foreach (var tab in tabsArray.EnumerateArray())
        {
            if (tab.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var key in new[] { "history", "History", "tabHistory", "TabHistory", "Transactions", "transactions", "Ledger", "ledger" })
            {
                if (TryGetPropertyIgnoreCase(tab, key, out var h) && h.ValueKind == JsonValueKind.Array)
                {
                    history += h.GetArrayLength();
                    break;
                }
            }
        }

        return history;
    }

    /// <summary>Heuristic: V2 sometimes persisted a single open tab as one object.</summary>
    public static bool TryRecognizeSingleTabObject(JsonElement root, out int historyLines)
    {
        historyLines = 0;
        if (root.ValueKind != JsonValueKind.Object || !LooksLikeTabRecord(root))
        {
            return false;
        }

        foreach (var key in new[] { "history", "History", "tabHistory", "TabHistory", "Transactions", "transactions" })
        {
            if (TryGetPropertyIgnoreCase(root, key, out var h) && h.ValueKind == JsonValueKind.Array)
            {
                historyLines = h.GetArrayLength();
                break;
            }
        }

        return true;
    }

    private static bool LooksLikeTabRecord(JsonElement o)
    {
        var score = 0;
        foreach (var name in new[] { "balance", "tabbalance", "memberid", "archived", "displayname", "name", "tabid", "id" })
        {
            if (TryGetPropertyIgnoreCase(o, name, out _))
            {
                score++;
            }
        }

        return score >= 2;
    }

    private static bool TryHintsThenLargest(JsonElement obj, LegacyJsonFileKind kind, string pathPrefix, out JsonElement array, out string matchedPath)
    {
        array = default;
        matchedPath = string.Empty;

        JsonElement? bestHintArray = null;
        string bestHintPath = string.Empty;
        var bestHintLen = -1;
        var bestHintRank = int.MaxValue;

        foreach (var hint in GetOrderedHints(kind))
        {
            if (!TryGetPropertyIgnoreCase(obj, hint, out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.Array)
            {
                var len = el.GetArrayLength();
                var rank = PropertyHintRank(GetCanonicalPropertyName(obj, hint), kind);
                if (len > bestHintLen || (len == bestHintLen && rank < bestHintRank))
                {
                    bestHintLen = len;
                    bestHintRank = rank;
                    bestHintArray = el;
                    bestHintPath = pathPrefix + GetCanonicalPropertyName(obj, hint);
                }
            }
            else if (el.ValueKind == JsonValueKind.Object)
            {
                if (TryHintsThenLargest(el, kind, pathPrefix + GetCanonicalPropertyName(obj, hint) + ".", out array, out matchedPath))
                {
                    return true;
                }
            }
        }

        if (bestHintArray is not null && bestHintLen >= 0)
        {
            array = bestHintArray.Value;
            matchedPath = bestHintPath;
            return true;
        }

        if (TryFindLargestArrayProperty(obj, out array, out var largestName))
        {
            matchedPath = pathPrefix + largestName + " (largest array)";
            return true;
        }

        return false;
    }

    private static string GetCanonicalPropertyName(JsonElement obj, string hintMatched)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, hintMatched, StringComparison.OrdinalIgnoreCase))
            {
                return p.Name;
            }
        }

        return hintMatched;
    }

    private static bool TryFindLargestArrayProperty(JsonElement obj, out JsonElement array, out string name)
    {
        array = default;
        name = string.Empty;
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var bestLen = -1;
        JsonElement best = default;
        var bestName = string.Empty;

        foreach (var p in obj.EnumerateObject())
        {
            if (p.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var len = p.Value.GetArrayLength();
            if (len > bestLen || (len == bestLen && string.Compare(p.Name, bestName, StringComparison.OrdinalIgnoreCase) < 0))
            {
                bestLen = len;
                best = p.Value;
                bestName = p.Name;
            }
        }

        if (bestLen < 0)
        {
            return false;
        }

        array = best;
        name = bestName;
        return true;
    }

    private static IEnumerable<string> GetOrderedHints(LegacyJsonFileKind kind)
    {
        IEnumerable<string> primary = kind switch
        {
            LegacyJsonFileKind.Drinks => new[]
            {
                "Drinks", "drinks", "DrinkList", "drinkList", "Beverages", "beverages", "DrinkMenu", "drinkMenu",
                "MenuDrinks", "menuDrinks", "BarItems", "barItems", "Products", "products",
            },
            LegacyJsonFileKind.Items => new[]
            {
                "StockItems", "stockItems", "Stock", "stock", "ProductItems", "productItems", "InventoryItems", "inventoryItems",
                "Items", "items", "ItemList", "itemList", "ProductList", "productList", "Catalog", "catalog",
                "MenuItems", "menuItems", "Skus", "skus", "Inventory", "inventory", "Products", "products",
            },
            LegacyJsonFileKind.Categories => new[]
            {
                "Categories", "categories", "CategoryList", "categoryList", "ItemCategories", "itemCategories",
                "ProductCategories", "productCategories", "Departments", "departments", "Groups", "groups",
                "MenuCategories", "menuCategories",
            },
            LegacyJsonFileKind.Tabs => new[]
            {
                "Tabs", "tabs", "OpenTabs", "openTabs", "TabList", "tabList", "ActiveTabs", "activeTabs",
                "CurrentTabs", "currentTabs", "ArchivedTabs", "archivedTabs", "TabData", "tabData", "TabCollection", "tabCollection",
            },
            LegacyJsonFileKind.Members => new[]
            {
                "Members", "members", "MemberList", "memberList", "Customers", "customers", "CustomerList", "customerList",
                "Patrons", "patrons", "Accounts", "accounts",
            },
            LegacyJsonFileKind.Bartenders => new[]
            {
                "Bartenders", "bartenders", "Users", "users", "Staff", "staff", "Operators", "operators",
                "Cashiers", "cashiers", "Employees", "employees", "People", "people",
            },
            LegacyJsonFileKind.PitstopSalesData => new[]
            {
                "PitstopSales", "pitstopSales", "PitstopSalesData", "pitstopSalesData", "Sales", "sales", "SaleList", "saleList",
                "Transactions", "transactions", "LineItems", "lineItems", "Orders", "orders", "OrderHistory", "orderHistory",
                "Receipts", "receipts",
            },
            _ => Array.Empty<string>(),
        };

        foreach (var h in primary)
        {
            yield return h;
        }

        foreach (var h in CommonArrayHints)
        {
            yield return h;
        }
    }
}

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Migration;

/// <summary>Deterministic synthetic legacy keys when V2 JSON omits stable identifiers (re-import safe).</summary>
public static class LegacyStableId
{
    public static string HashHex(string material)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(bytes)[..32];
    }

    /// <summary>Identity excludes balance so tab upserts stay stable as balances change.</summary>
    public static string ForTab(LegacyTabDto dto)
    {
        var name = dto.Name ?? string.Empty;
        var display = dto.DisplayName ?? string.Empty;
        var member = dto.MemberId ?? string.Empty;
        var arch = (dto.Archived ?? dto.IsArchived ?? false).ToString(CultureInfo.InvariantCulture);
        return "syn_t_" + HashHex($"{name}\u001f{display}\u001f{member}\u001f{arch}");
    }

    /// <summary>Identity excludes stock/price so re-import maps to the same item row.</summary>
    public static string ForItem(LegacyItemDto dto)
    {
        var name = dto.Name ?? string.Empty;
        var cat = dto.Category ?? string.Empty;
        var sub = dto.SubCategory ?? string.Empty;
        var sku = dto.Sku ?? dto.Barcode ?? dto.ProductCode ?? string.Empty;
        return "syn_i_" + HashHex($"{name}\u001f{cat}\u001f{sub}\u001f{sku}");
    }

    public static string ForDrink(LegacyDrinkDto dto)
    {
        var name = dto.Name ?? string.Empty;
        var sku = dto.Sku ?? dto.Barcode ?? string.Empty;
        var cat = dto.Category ?? string.Empty;
        return "syn_d_" + HashHex($"{name}\u001f{sku}\u001f{cat}");
    }

    public static string ForCategory(LegacyCategoryDto dto)
    {
        var slug = dto.Key ?? dto.Name ?? string.Empty;
        var display = dto.DisplayName ?? string.Empty;
        var id = dto.Id ?? string.Empty;
        return "syn_cat_" + HashHex($"{slug}\u001f{display}\u001f{id}");
    }

    /// <summary>Identity excludes balance.</summary>
    public static string ForMember(LegacyMemberDto dto)
    {
        var name = dto.Name ?? string.Empty;
        var email = dto.Email ?? string.Empty;
        var phone = dto.Phone ?? string.Empty;
        return "syn_m_" + HashHex($"{name}\u001f{email}\u001f{phone}");
    }

    public static string ForBartender(LegacyBartenderDto dto)
    {
        var name = dto.Name ?? string.Empty;
        var role = dto.Role ?? string.Empty;
        return "syn_b_" + HashHex($"{name}\u001f{role}");
    }

    public static string ForPitstopSale(LegacyPitstopSaleDto dto)
    {
        var sku = dto.Sku ?? string.Empty;
        var item = dto.ItemName ?? string.Empty;
        var qty = (dto.Quantity ?? 0).ToString(CultureInfo.InvariantCulture);
        var total = (dto.Total ?? 0m).ToString(CultureInfo.InvariantCulture);
        var sold = dto.SoldAt ?? string.Empty;
        return "syn_p_" + HashHex($"{sold}\u001f{sku}\u001f{item}\u001f{qty}\u001f{total}");
    }

    public static string ForTabHistoryEntry(string tabLegacyId, int index, LegacyTabHistoryEntryDto h)
    {
        var type = h.Type ?? string.Empty;
        var ts = h.Timestamp ?? string.Empty;
        var amt = (h.Amount ?? 0m).ToString(CultureInfo.InvariantCulture);
        var note = h.Note ?? string.Empty;
        var id = h.Id ?? string.Empty;
        return "syn_e_" + HashHex($"{tabLegacyId}\u001f{index}\u001f{id}\u001f{type}\u001f{ts}\u001f{amt}\u001f{note}");
    }
}

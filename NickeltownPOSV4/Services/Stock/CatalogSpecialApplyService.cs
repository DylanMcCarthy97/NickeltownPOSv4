using System;
using System.Globalization;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Stock;

public static class CatalogSpecialApplyService
{
    public static bool TryApplySpecialToDetail(
        StockManagementPageViewModel vm,
        bool enabled,
        string specialType,
        string specialValueText,
        string appliesToMode,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        vm.DetailIsOnSpecial = enabled;
        vm.SpecialType = CatalogSpecialValueResolver.NormalizeType(specialType);
        vm.SpecialValueText = (specialValueText ?? string.Empty).Trim();
        vm.SpecialAppliesToMode = NormalizeAppliesTo(appliesToMode);

        if (!enabled)
        {
            vm.DetailBarSpecialText = string.Empty;
            vm.DetailGuestSpecialText = string.Empty;
            vm.DetailPitstopSpecialText = string.Empty;
            return true;
        }

        var mode = vm.SpecialAppliesToMode;
        if (mode == "Bar")
        {
            return TrySetBarChannel(vm, out errorMessage);
        }

        if (mode == "Pitstop")
        {
            return TrySetPitstopChannel(vm, out errorMessage);
        }

        if (!TrySetBarChannel(vm, out errorMessage))
        {
            return false;
        }

        return TrySetPitstopChannel(vm, out errorMessage);
    }

    private static bool TrySetBarChannel(StockManagementPageViewModel vm, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!TryParseRegular(vm.BarPriceText, out var barRegular))
        {
            errorMessage = "Enter a valid bar price before saving this special.";
            return false;
        }

        if (!CatalogSpecialValueResolver.TryResolveSaleUnitPrice(
                vm.SpecialType,
                vm.SpecialValueText,
                barRegular,
                out var barSale,
                out errorMessage))
        {
            return false;
        }

        vm.DetailBarSpecialText = FormatMoney(barSale);

        if (TryParseRegular(vm.DetailGuestPriceText, out var guestRegular) && guestRegular > 0m)
        {
            if (!CatalogSpecialValueResolver.TryResolveSaleUnitPrice(
                    vm.SpecialType,
                    vm.SpecialValueText,
                    guestRegular,
                    out var guestSale,
                    out errorMessage))
            {
                return false;
            }

            vm.DetailGuestSpecialText = FormatMoney(guestSale);
        }
        else
        {
            vm.DetailGuestSpecialText = string.Empty;
        }

        return true;
    }

    private static bool TrySetPitstopChannel(StockManagementPageViewModel vm, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!TryParseRegular(vm.PitstopPriceText, out var pitRegular))
        {
            errorMessage = "Enter a valid Pitstop / retail price before saving this special.";
            return false;
        }

        if (!CatalogSpecialValueResolver.TryResolveSaleUnitPrice(
                vm.SpecialType,
                vm.SpecialValueText,
                pitRegular,
                out var pitSale,
                out errorMessage))
        {
            return false;
        }

        vm.DetailPitstopSpecialText = FormatMoney(pitSale);
        return true;
    }

    private static bool TryParseRegular(string? text, out decimal regular)
    {
        regular = 0m;
        return StockMoneyInputParser.TryParseMoney(text, out regular) && regular > 0m;
    }

    private static string FormatMoney(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string NormalizeAppliesTo(string? mode)
    {
        var m = (mode ?? string.Empty).Trim();
        if (string.Equals(m, "Pitstop", StringComparison.OrdinalIgnoreCase))
        {
            return "Pitstop";
        }

        if (string.Equals(m, "Both", StringComparison.OrdinalIgnoreCase))
        {
            return "Both";
        }

        return "Bar";
    }
}

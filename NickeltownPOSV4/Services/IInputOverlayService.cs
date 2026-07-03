using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Controls;

namespace NickeltownPOSV4.Services;

public interface IInputOverlayService
{
    void Attach(InputOverlayHost host);

    bool IsOpen { get; }

    void Close();

    Task<decimal?> ShowNumpadAsync(
        decimal initialValue,
        string title,
        bool allowSignedAmount = false,
        CancellationToken cancellationToken = default);

    Task<string?> ShowKeyboardAsync(string initialValue, string title, CancellationToken cancellationToken = default);

    /// <summary>Whole-number numeric entry (no decimal/sign). Returns null on cancel, or the value clamped to [<paramref name="min"/>, <paramref name="max"/>].</summary>
    Task<int?> ShowIntegerNumpadAsync(
        int initialValue,
        string title,
        int min = 0,
        int max = int.MaxValue,
        CancellationToken cancellationToken = default);

    /// <summary>Fixed-length numeric PIN entry. Returns the digit string (length == <paramref name="digitCount"/>) or null on cancel.</summary>
    Task<string?> ShowPinNumpadAsync(
        string title,
        int digitCount = 4,
        bool maskDisplay = true,
        CancellationToken cancellationToken = default);

    /// <summary>Variable-length digit string entry (e.g. phone). Returns null on cancel, or the entered digits (may be empty when <paramref name="allowEmpty"/> is true).</summary>
    Task<string?> ShowDigitStringNumpadAsync(
        string initialValue,
        string title,
        int maxLength = 15,
        bool allowEmpty = true,
        CancellationToken cancellationToken = default);

    /// <summary>Touch-friendly day/month/year picker. Cancelled=true leaves the field unchanged; cleared sets Value to null.</summary>
    Task<DatePickerOverlayResult> ShowDatePickerAsync(
        DateOnly? initialValue,
        string title,
        int? minYear = null,
        int? maxYear = null,
        CancellationToken cancellationToken = default);

}

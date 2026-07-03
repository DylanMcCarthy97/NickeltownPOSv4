using System;

namespace NickeltownPOSV4.Services;

/// <summary>Result from <see cref="IInputOverlayService.ShowDatePickerAsync"/>.</summary>
public readonly record struct DatePickerOverlayResult(bool Cancelled, DateOnly? Value)
{
    public bool IsCleared => !Cancelled && !Value.HasValue;

    public bool HasSelection => !Cancelled && Value.HasValue;
}

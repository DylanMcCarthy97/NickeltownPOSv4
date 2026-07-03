namespace NickeltownPOSV4;

/// <summary>TCxWave window placement. Toggle <see cref="UseKioskPinnedPosition"/> for kiosk vs dev machine.</summary>
public static class KioskDisplayOptions
{
    /// <summary>
    /// When true: 1024×768 window pinned to (0,0). When false: same size, centered on the current monitor work area.
    /// </summary>
    public static bool UseKioskPinnedPosition { get; set; }

    public const int TargetWindowWidth = 1024;

    public const int TargetWindowHeight = 768;
}

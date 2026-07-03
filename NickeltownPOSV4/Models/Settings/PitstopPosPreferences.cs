namespace NickeltownPOSV4.Models.Settings;

/// <summary>Persisted Pitstop POS checkout preferences (SQLite Settings table).</summary>
public sealed class PitstopPosPreferences
{
    /// <summary>Pass-through card processing percent shown at checkout (e.g. 1.7 = 1.7% added to card total).</summary>
    public decimal CardSurchargePercent { get; set; } = 1.7m;
}

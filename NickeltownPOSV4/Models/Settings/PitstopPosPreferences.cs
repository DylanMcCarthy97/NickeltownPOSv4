namespace NickeltownPOSV4.Models.Settings;

/// <summary>Persisted Pitstop POS checkout preferences (SQLite Settings table).</summary>
public sealed class PitstopPosPreferences
{
    /// <summary>Legacy field. New card charges do not add a customer surcharge.</summary>
    public decimal CardSurchargePercent { get; set; }
}

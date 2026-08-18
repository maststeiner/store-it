namespace StoreIt.Domain;

/// <summary>
/// Expiry grouping of an item (SPEC-001 AC-10 to AC-12).
/// </summary>
public enum ExpiryStatus
{
    /// <summary>No warning — also applies to items without an expiry date (EC-05).</summary>
    Ok,

    /// <summary>Expiry date within the next <see cref="ExpiryRules.ExpiringSoonThresholdDays"/> days, including today (AC-11, EC-02).</summary>
    ExpiringSoon,

    /// <summary>Expiry date in the past (AC-12).</summary>
    Expired,
}

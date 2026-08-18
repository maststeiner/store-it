namespace StoreIt.Domain;

/// <summary>
/// Expiry logic per SPEC-001. The threshold is a named domain constant —
/// not hard-coded in any client (technical constraint in SPEC-001).
/// </summary>
public static class ExpiryRules
{
    public const int ExpiringSoonThresholdDays = 3;

    /// <summary>
    /// Determines the expiry status of an item (AC-10 to AC-12, EC-02, EC-05).
    /// </summary>
    /// <param name="expiryDate">The item's expiry date; null when only a production date was recorded.</param>
    /// <param name="today">The current date.</param>
    public static ExpiryStatus GetStatus(DateOnly? expiryDate, DateOnly today)
    {
        if (expiryDate is null)
        {
            return ExpiryStatus.Ok;
        }

        if (expiryDate.Value < today)
        {
            return ExpiryStatus.Expired;
        }

        return expiryDate.Value.DayNumber - today.DayNumber <= ExpiringSoonThresholdDays
            ? ExpiryStatus.ExpiringSoon
            : ExpiryStatus.Ok;
    }
}

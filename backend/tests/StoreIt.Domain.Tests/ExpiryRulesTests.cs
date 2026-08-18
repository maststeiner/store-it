using StoreIt.Domain;

namespace StoreIt.Domain.Tests;

/// <summary>
/// Derived from SPEC-001 acceptance criteria (AC-11, AC-12) and edge cases (EC-02, EC-05) —
/// not from the implementation.
/// </summary>
public class ExpiryRulesTests
{
    private static readonly DateOnly Today = new(2026, 7, 13);

    [Fact]
    public void GetStatus_ExpiryDateInThePast_ReturnsExpired()
    {
        // AC-12
        var status = ExpiryRules.GetStatus(Today.AddDays(-1), Today);

        Assert.Equal(ExpiryStatus.Expired, status);
    }

    [Fact]
    public void GetStatus_ExpiryDateToday_ReturnsExpiringSoon()
    {
        // EC-02: expiry exactly today → "expiring soon", not "expired"
        var status = ExpiryRules.GetStatus(Today, Today);

        Assert.Equal(ExpiryStatus.ExpiringSoon, status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetStatus_ExpiryDateWithinThreshold_ReturnsExpiringSoon(int daysAhead)
    {
        // AC-11: within the next 3 days
        var status = ExpiryRules.GetStatus(Today.AddDays(daysAhead), Today);

        Assert.Equal(ExpiryStatus.ExpiringSoon, status);
    }

    [Fact]
    public void GetStatus_ExpiryDateBeyondThreshold_ReturnsOk()
    {
        // 4 days is deliberately hard-coded: SPEC-001 fixes the threshold at 3 days.
        // If the production constant changes, this test must fail (spec first, then code).
        var status = ExpiryRules.GetStatus(Today.AddDays(4), Today);

        Assert.Equal(ExpiryStatus.Ok, status);
    }

    [Fact]
    public void GetStatus_NoExpiryDate_ReturnsOk()
    {
        // EC-05: item with only a production date is never marked expired/expiring soon
        var status = ExpiryRules.GetStatus(null, Today);

        Assert.Equal(ExpiryStatus.Ok, status);
    }
}

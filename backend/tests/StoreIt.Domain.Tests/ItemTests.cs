using StoreIt.Domain;

namespace StoreIt.Domain.Tests;

/// <summary>
/// Derived from SPEC-001 acceptance criteria (AC-07, AC-11, AC-12) and edge cases
/// (EC-02, EC-05) at the Item level — not from the implementation.
/// ExpiryRulesTests already covers the raw status calculation; here we verify that
/// an Item exposes the same spec behavior through its public API.
/// </summary>
public class ItemTests
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly Guid AnyOwner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Item CreateItem(DateOnly? expiryDate, DateOnly? productionDate) =>
        Storage
            .Create("Pantry", AnyOwner)
            .AddItem("Milk", 1m, Unit.Liter, expiryDate, productionDate);

    // --- Unit validation (AC-06) ---

    [Fact]
    public void AddItem_WithUndefinedUnit_ThrowsDomainValidationException()
    {
        // AC-06: unit must be from the fixed list; an out-of-range enum value is rejected
        var storage = Storage.Create("Pantry", AnyOwner);

        var exception = Assert.Throws<DomainValidationException>(() =>
            storage.AddItem("Mystery", 1m, (Unit)999, Today.AddDays(5), null)
        );
        Assert.Equal("item.unit.invalid", exception.ErrorCode);
    }

    // --- Expiry status via Item (AC-11, AC-12, EC-02, EC-05) ---

    [Fact]
    public void GetExpiryStatus_ExpiryDateInThePast_ReturnsExpired()
    {
        // AC-12
        var item = CreateItem(Today.AddDays(-1), null);

        Assert.Equal(ExpiryStatus.Expired, item.GetExpiryStatus(Today));
    }

    [Fact]
    public void GetExpiryStatus_ExpiryDateToday_ReturnsExpiringSoon()
    {
        // AC-11 / EC-02: today counts as "expiring soon", not "expired"
        var item = CreateItem(Today, null);

        Assert.Equal(ExpiryStatus.ExpiringSoon, item.GetExpiryStatus(Today));
    }

    [Fact]
    public void GetExpiryStatus_ExpiryDateBeyondThreshold_ReturnsOk()
    {
        var item = CreateItem(Today.AddDays(4), null);

        Assert.Equal(ExpiryStatus.Ok, item.GetExpiryStatus(Today));
    }

    [Fact]
    public void GetExpiryStatus_OnlyProductionDate_ReturnsOk()
    {
        // EC-05: never "expired"/"expiring soon" without an expiry date
        var item = CreateItem(null, Today.AddDays(-30));

        Assert.Equal(ExpiryStatus.Ok, item.GetExpiryStatus(Today));
    }

    // --- Update (AC-07) ---

    [Fact]
    public void Update_WithValidData_UpdatesAllFields()
    {
        // AC-07
        var item = CreateItem(Today, null);

        item.Update("Oat Milk", 2.5m, Unit.Pack, Today.AddDays(5), Today.AddDays(-1));

        Assert.Equal("Oat Milk", item.Name);
        Assert.Equal(2.5m, item.Amount);
        Assert.Equal(Unit.Pack, item.Unit);
        Assert.Equal(Today.AddDays(5), item.ExpiryDate);
        Assert.Equal(Today.AddDays(-1), item.ProductionDate);
    }

    [Fact]
    public void Update_WithNegativeAmount_ThrowsDomainValidationException()
    {
        // AC-07 (same validation as AC-06)
        var item = CreateItem(Today, null);

        var exception = Assert.Throws<DomainValidationException>(() =>
            item.Update("Milk", -1m, Unit.Liter, Today, null)
        );

        Assert.Equal("item.amount.notPositive", exception.ErrorCode);
    }

    [Fact]
    public void Update_WithNeitherDate_ThrowsDomainValidationException()
    {
        // AC-07 (same validation as AC-06)
        var item = CreateItem(Today, null);

        var exception = Assert.Throws<DomainValidationException>(() =>
            item.Update("Milk", 1m, Unit.Liter, null, null)
        );

        Assert.Equal("item.dates.missing", exception.ErrorCode);
    }
}

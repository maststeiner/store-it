using StoreIt.Domain;

namespace StoreIt.Domain.Tests;

/// <summary>
/// Derived from SPEC-001 acceptance criteria (AC-01..AC-10) and edge cases
/// (EC-01, EC-03, EC-04) — not from the implementation.
/// </summary>
public class StorageTests
{
    private static readonly DateOnly AnyDate = new(2026, 7, 13);
    private static readonly Guid AnyOwner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // --- Storage creation (AC-01, AC-02) ---

    [Fact]
    public void Create_WithValidName_SetsNameAndStartsWithoutItems()
    {
        // AC-01
        var storage = Storage.Create("Pantry", AnyOwner);

        Assert.NotEqual(Guid.Empty, storage.Id);
        Assert.Equal("Pantry", storage.Name);
        Assert.Empty(storage.Items);
    }

    [Fact]
    public void Create_WithOwner_SetsOwnerId()
    {
        // SPEC-003: the storage is stamped with its owner.
        var storage = Storage.Create("Pantry", AnyOwner);

        Assert.Equal(AnyOwner, storage.OwnerId);
    }

    [Fact]
    public void Create_WithEmptyOwner_Throws()
    {
        // SPEC-003: an owner is mandatory.
        var exception = Assert.Throws<DomainValidationException>(() =>
            Storage.Create("Pantry", Guid.Empty)
        );

        Assert.Equal("storage.owner.missing", exception.ErrorCode);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsDomainValidationException()
    {
        // AC-02
        var exception = Assert.Throws<DomainValidationException>(() =>
            Storage.Create("", AnyOwner)
        );

        Assert.Equal("storage.name.empty", exception.ErrorCode);
    }

    // --- Rename (AC-03) ---

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        // AC-03
        var storage = Storage.Create("Pantry", AnyOwner);

        storage.Rename("Cellar");

        Assert.Equal("Cellar", storage.Name);
    }

    [Fact]
    public void Rename_WithEmptyName_ThrowsDomainValidationException()
    {
        // AC-03 (same validation as AC-02)
        var storage = Storage.Create("Pantry", AnyOwner);

        var exception = Assert.Throws<DomainValidationException>(() => storage.Rename(""));

        Assert.Equal("storage.name.empty", exception.ErrorCode);
    }

    // --- AddItem happy paths (AC-05) ---

    [Fact]
    public void AddItem_WithValidDataAndExpiryDate_AddsItemToStorage()
    {
        // AC-05: at least one date — expiry date only
        var storage = Storage.Create("Pantry", AnyOwner);

        var item = storage.AddItem("Milk", 1.5m, Unit.Liter, AnyDate, null);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("Milk", item.Name);
        Assert.Equal(1.5m, item.Amount);
        Assert.Equal(Unit.Liter, item.Unit);
        Assert.Equal(AnyDate, item.ExpiryDate);
        Assert.Null(item.ProductionDate);
        Assert.Contains(item, storage.Items);
    }

    [Fact]
    public void AddItem_WithOnlyProductionDate_AddsItemToStorage()
    {
        // AC-05: at least one date — production date only
        var storage = Storage.Create("Pantry", AnyOwner);

        var item = storage.AddItem("Flour", 1m, Unit.Kilogram, null, AnyDate);

        Assert.Null(item.ExpiryDate);
        Assert.Equal(AnyDate, item.ProductionDate);
        Assert.Contains(item, storage.Items);
    }

    [Fact]
    public void AddItem_WithBothDates_AddsItemToStorage()
    {
        // AC-05: both dates allowed
        var storage = Storage.Create("Pantry", AnyOwner);

        var item = storage.AddItem("Yogurt", 4m, Unit.Piece, AnyDate.AddDays(7), AnyDate);

        Assert.Equal(AnyDate.AddDays(7), item.ExpiryDate);
        Assert.Equal(AnyDate, item.ProductionDate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0.5)]
    [InlineData(12.5)]
    [InlineData(100)]
    public void AddItem_WithAtMostOneDecimalPlace_AddsItem(double amount)
    {
        // AC-05 / EC-04 boundary: up to one decimal place is valid
        var storage = Storage.Create("Pantry", AnyOwner);

        var item = storage.AddItem("Rice", (decimal)amount, Unit.Gram, AnyDate, null);

        Assert.Equal((decimal)amount, item.Amount);
    }

    // --- AddItem validation (AC-06, EC-04) ---

    [Fact]
    public void AddItem_WithEmptyName_ThrowsDomainValidationException()
    {
        // AC-06: empty name
        var storage = Storage.Create("Pantry", AnyOwner);

        var exception = Assert.Throws<DomainValidationException>(() =>
            storage.AddItem("", 1m, Unit.Piece, AnyDate, null)
        );

        Assert.False(string.IsNullOrEmpty(exception.ErrorCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void AddItem_WithNonPositiveAmount_ThrowsDomainValidationException(double amount)
    {
        // AC-06: amount ≤ 0
        var storage = Storage.Create("Pantry", AnyOwner);

        var exception = Assert.Throws<DomainValidationException>(() =>
            storage.AddItem("Milk", (decimal)amount, Unit.Piece, AnyDate, null)
        );

        Assert.Equal("item.amount.notPositive", exception.ErrorCode);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1.001)]
    [InlineData(99.99)]
    public void AddItem_WithMoreThanOneDecimalPlace_ThrowsDomainValidationException(double amount)
    {
        // AC-06 / EC-04: more than one decimal place → validation error, no silent rounding
        var storage = Storage.Create("Pantry", AnyOwner);

        var exception = Assert.Throws<DomainValidationException>(() =>
            storage.AddItem("Butter", (decimal)amount, Unit.Gram, AnyDate, null)
        );

        Assert.Equal("item.amount.tooManyDecimals", exception.ErrorCode);
    }

    [Fact]
    public void AddItem_WithNeitherDate_ThrowsDomainValidationException()
    {
        // AC-06: at least one of expiry / production date is required
        var storage = Storage.Create("Pantry", AnyOwner);

        var exception = Assert.Throws<DomainValidationException>(() =>
            storage.AddItem("Milk", 1m, Unit.Piece, null, null)
        );

        Assert.Equal("item.dates.missing", exception.ErrorCode);
    }

    // --- Duplicate names (EC-01) ---

    [Fact]
    public void AddItem_WithSameNameTwice_KeepsSeparateItems()
    {
        // EC-01: two items with the same name are separate items
        var storage = Storage.Create("Pantry", AnyOwner);

        var first = storage.AddItem("Yogurt", 1m, Unit.Piece, AnyDate.AddDays(2), null);
        var second = storage.AddItem("Yogurt", 1m, Unit.Piece, AnyDate.AddDays(9), null);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, storage.Items.Count);
    }

    // --- UpdateItem (AC-07, AC-08) ---

    [Fact]
    public void UpdateItem_WithValidData_UpdatesAllFieldsAndReturnsTrue()
    {
        // AC-07
        var storage = Storage.Create("Pantry", AnyOwner);
        var item = storage.AddItem("Milk", 1m, Unit.Liter, AnyDate, null);

        var kept = storage.UpdateItem(
            item.Id,
            "Oat Milk",
            2.5m,
            Unit.Pack,
            AnyDate.AddDays(5),
            AnyDate.AddDays(-1)
        );

        Assert.True(kept);
        var updated = Assert.Single(storage.Items);
        Assert.Equal("Oat Milk", updated.Name);
        Assert.Equal(2.5m, updated.Amount);
        Assert.Equal(Unit.Pack, updated.Unit);
        Assert.Equal(AnyDate.AddDays(5), updated.ExpiryDate);
        Assert.Equal(AnyDate.AddDays(-1), updated.ProductionDate);
    }

    [Fact]
    public void UpdateItem_WithEmptyName_ThrowsDomainValidationException()
    {
        // AC-07 (same validation as AC-06)
        var storage = Storage.Create("Pantry", AnyOwner);
        var item = storage.AddItem("Milk", 1m, Unit.Liter, AnyDate, null);

        Assert.Throws<DomainValidationException>(() =>
            storage.UpdateItem(item.Id, "", 1m, Unit.Liter, AnyDate, null)
        );
    }

    [Fact]
    public void UpdateItem_WithNegativeAmount_ThrowsDomainValidationException()
    {
        // AC-07: negative amount is rejected (0 means removal per AC-08, below 0 is invalid)
        var storage = Storage.Create("Pantry", AnyOwner);
        var item = storage.AddItem("Milk", 1m, Unit.Liter, AnyDate, null);

        var exception = Assert.Throws<DomainValidationException>(() =>
            storage.UpdateItem(item.Id, "Milk", -1m, Unit.Liter, AnyDate, null)
        );

        Assert.Equal("item.amount.notPositive", exception.ErrorCode);
    }

    [Fact]
    public void UpdateItem_WithMoreThanOneDecimalPlace_ThrowsDomainValidationException()
    {
        // AC-07 / EC-04
        var storage = Storage.Create("Pantry", AnyOwner);
        var item = storage.AddItem("Milk", 1m, Unit.Liter, AnyDate, null);

        var exception = Assert.Throws<DomainValidationException>(() =>
            storage.UpdateItem(item.Id, "Milk", 0.25m, Unit.Liter, AnyDate, null)
        );

        Assert.Equal("item.amount.tooManyDecimals", exception.ErrorCode);
    }

    [Fact]
    public void UpdateItem_WithNeitherDate_ThrowsDomainValidationException()
    {
        // AC-07 (same validation as AC-06)
        var storage = Storage.Create("Pantry", AnyOwner);
        var item = storage.AddItem("Milk", 1m, Unit.Liter, AnyDate, null);

        var exception = Assert.Throws<DomainValidationException>(() =>
            storage.UpdateItem(item.Id, "Milk", 1m, Unit.Liter, null, null)
        );

        Assert.Equal("item.dates.missing", exception.ErrorCode);
    }

    [Fact]
    public void UpdateItem_WithAmountZero_RemovesItemAndReturnsFalse()
    {
        // AC-08: setting amount to 0 removes the item
        var storage = Storage.Create("Pantry", AnyOwner);
        var item = storage.AddItem("Milk", 1m, Unit.Liter, AnyDate, null);

        var kept = storage.UpdateItem(item.Id, "Milk", 0m, Unit.Liter, AnyDate, null);

        Assert.False(kept);
        Assert.Empty(storage.Items);
    }

    [Fact]
    public void UpdateItem_WithUnknownItemId_ThrowsItemNotFoundException()
    {
        var storage = Storage.Create("Pantry", AnyOwner);

        Assert.Throws<ItemNotFoundException>(() =>
            storage.UpdateItem(Guid.NewGuid(), "Milk", 1m, Unit.Liter, AnyDate, null)
        );
    }

    // --- RemoveItem (AC-09) ---

    [Fact]
    public void RemoveItem_ExistingItem_RemovesItRegardlessOfAmount()
    {
        // AC-09
        var storage = Storage.Create("Pantry", AnyOwner);
        var item = storage.AddItem("Milk", 3m, Unit.Liter, AnyDate, null);

        storage.RemoveItem(item.Id);

        Assert.Empty(storage.Items);
    }

    [Fact]
    public void RemoveItem_UnknownItemId_ThrowsItemNotFoundException()
    {
        var storage = Storage.Create("Pantry", AnyOwner);

        Assert.Throws<ItemNotFoundException>(() => storage.RemoveItem(Guid.NewGuid()));
    }

    // --- Sorting (AC-10, EC-03) ---

    [Fact]
    public void GetItemsSortedByExpiry_EmptyStorage_ReturnsEmptyList()
    {
        // EC-03: storage with 0 items → empty list, no error
        var storage = Storage.Create("Pantry", AnyOwner);

        var sorted = storage.GetItemsSortedByExpiry();

        Assert.Empty(sorted);
    }

    [Fact]
    public void GetItemsSortedByExpiry_MixedItems_SortsByExpiryAscendingWithItemsWithoutExpiryLast()
    {
        // AC-10: sorted by expiry date ascending, items without expiry date last
        var storage = Storage.Create("Pantry", AnyOwner);
        var withoutExpiry = storage.AddItem("Flour", 1m, Unit.Kilogram, null, AnyDate);
        var late = storage.AddItem("Cheese", 1m, Unit.Piece, AnyDate.AddDays(10), null);
        var early = storage.AddItem("Milk", 1m, Unit.Liter, AnyDate.AddDays(1), null);
        var expired = storage.AddItem("Yogurt", 1m, Unit.Piece, AnyDate.AddDays(-2), null);

        var sorted = storage.GetItemsSortedByExpiry();

        Assert.Equal(
            new[] { expired.Id, early.Id, late.Id, withoutExpiry.Id },
            sorted.Select(item => item.Id)
        );
    }
}

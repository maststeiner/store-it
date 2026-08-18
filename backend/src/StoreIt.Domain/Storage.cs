namespace StoreIt.Domain;

/// <summary>
/// Aggregate root (SPEC-001): a named object holding a list of items.
/// </summary>
public class Storage
{
    private readonly List<Item> _items = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>SPEC-003: the owning user. Every storage belongs to exactly one owner.</summary>
    public Guid OwnerId { get; private set; }

    public IReadOnlyCollection<Item> Items => _items.AsReadOnly();

    private Storage() { } // EF Core

    private Storage(string name, Guid ownerId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainValidationException(
                "storage.owner.missing",
                "Storage owner must be provided."
            );
        }

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Rename(name);
    }

    /// <summary>
    /// AC-01/AC-02: create a storage with a non-empty name, owned by
    /// <paramref name="ownerId"/> (SPEC-003).
    /// </summary>
    public static Storage Create(string name, Guid ownerId) => new(name, ownerId);

    /// <summary>AC-03: rename (same validation as AC-02).</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "storage.name.empty",
                "Storage name must not be empty."
            );
        }

        Name = name.Trim();
    }

    /// <summary>AC-05/AC-06: add an item (validation inside <see cref="Item"/>).</summary>
    public Item AddItem(
        string name,
        decimal amount,
        Unit unit,
        DateOnly? expiryDate,
        DateOnly? productionDate
    )
    {
        var item = new Item(name, amount, unit, expiryDate, productionDate);
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// AC-07/AC-08: update an item; an amount of 0 removes it from the storage.
    /// Returns false when the amount reached 0 and the item was removed.
    /// </summary>
    public bool UpdateItem(
        Guid itemId,
        string name,
        decimal amount,
        Unit unit,
        DateOnly? expiryDate,
        DateOnly? productionDate
    )
    {
        var item = GetItem(itemId);

        // AC-08: amount 0 on the edit path removes the item. Negative amounts are
        // a validation error (EC-04 analog) handled by Item.Update below.
        if (amount == 0)
        {
            _items.Remove(item);
            return false;
        }

        item.Update(name, amount, unit, expiryDate, productionDate);
        return true;
    }

    /// <summary>AC-09: delete an item regardless of amount.</summary>
    public void RemoveItem(Guid itemId) => _items.Remove(GetItem(itemId));

    /// <summary>
    /// AC-10: items sorted by expiry date ascending; items without expiry date last
    /// (EC-05: those carry only a production date and are never marked expired).
    /// </summary>
    public IReadOnlyList<Item> GetItemsSortedByExpiry() =>
        _items.OrderBy(i => i.ExpiryDate ?? DateOnly.MaxValue).ThenBy(i => i.Name).ToList();

    private Item GetItem(Guid itemId) =>
        _items.FirstOrDefault(i => i.Id == itemId) ?? throw new ItemNotFoundException(itemId);
}

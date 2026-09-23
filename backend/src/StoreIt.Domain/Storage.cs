namespace StoreIt.Domain;

/// <summary>
/// Aggregate root (SPEC-001): a named object holding a list of items.
/// </summary>
public class Storage
{
    private readonly List<Item> _items = [];
    private readonly List<StorageMember> _members = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>SPEC-003: the owning user. Every storage belongs to exactly one owner.</summary>
    public Guid OwnerId { get; private set; }

    public IReadOnlyCollection<Item> Items => _items.AsReadOnly();

    /// <summary>SPEC-007: users besides the owner who work on this storage (ADR-008).</summary>
    public IReadOnlyCollection<StorageMember> Members => _members.AsReadOnly();

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

    /// <summary>SPEC-007: the one user allowed to delete, share and hand over (ADR-008 D1).</summary>
    public bool IsOwner(Guid userId) => OwnerId == userId;

    /// <summary>SPEC-007: owner or member — everyone who may read and change the contents.</summary>
    public bool HasAccess(Guid userId) => IsOwner(userId) || IsMember(userId);

    public bool IsMember(Guid userId) => _members.Any(m => m.UserId == userId);

    /// <summary>
    /// SPEC-007 AC-09: add a member. Idempotent: the owner and existing members are left as
    /// they are and <c>false</c> is returned.
    /// </summary>
    public bool AddMember(Guid userId, DateTimeOffset joinedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException(
                "storage.member.missing",
                "Member user id must be provided."
            );
        }

        if (HasAccess(userId))
        {
            return false;
        }

        _members.Add(new StorageMember(Id, userId, joinedAt));
        return true;
    }

    /// <summary>SPEC-007 AC-12/AC-13: end a membership. Returns false when there was none.</summary>
    public bool RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        return member is not null && _members.Remove(member);
    }

    /// <summary>
    /// SPEC-007 AC-18: hand the storage to a member. The previous owner becomes an ordinary
    /// member; the storage never has zero or two owners (one column, one transaction).
    /// Returns false when <paramref name="newOwnerId"/> is not a member.
    /// </summary>
    public bool TransferOwnership(Guid newOwnerId, DateTimeOffset now)
    {
        if (IsOwner(newOwnerId))
        {
            return true;
        }

        var newOwner = _members.FirstOrDefault(m => m.UserId == newOwnerId);
        if (newOwner is null)
        {
            return false;
        }

        _members.Remove(newOwner);
        _members.Add(new StorageMember(Id, OwnerId, now));
        OwnerId = newOwnerId;
        return true;
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

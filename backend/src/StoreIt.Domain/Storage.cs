namespace StoreIt.Domain;

/// <summary>
/// Aggregate root (SPEC-001): a named object holding a list of items.
/// </summary>
public class Storage
{
    private readonly List<Item> _items = [];
    private readonly List<StorageMember> _members = [];
    private readonly List<Tag> _tags = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>SPEC-003: the owning user. Every storage belongs to exactly one owner.</summary>
    public Guid OwnerId { get; private set; }

    public IReadOnlyCollection<Item> Items => _items.AsReadOnly();

    /// <summary>SPEC-007: users besides the owner who work on this storage (ADR-008).</summary>
    public IReadOnlyCollection<StorageMember> Members => _members.AsReadOnly();

    /// <summary>SPEC-011 D1/D2: the tags in use on this storage's items — never more, never fewer.</summary>
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

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

    /// <summary>
    /// AC-05/AC-06: add an item (validation inside <see cref="Item"/>); SPEC-011 AC-01/AC-02:
    /// with its tags, resolved against the storage's existing ones.
    /// </summary>
    public Item AddItem(
        string name,
        decimal amount,
        Unit unit,
        DateOnly? expiryDate,
        DateOnly? productionDate,
        IEnumerable<string?>? tags = null
    )
    {
        var item = new Item(name, amount, unit, expiryDate, productionDate);
        item.SetTags(ResolveTags(tags));
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
        DateOnly? productionDate,
        IEnumerable<string?>? tags = null
    )
    {
        var item = GetItem(itemId);

        // AC-08: amount 0 on the edit path removes the item. Negative amounts are
        // a validation error (EC-04 analog) handled by Item.Update below.
        if (amount == 0)
        {
            _items.Remove(item);
            PruneUnusedTags();
            return false;
        }

        item.Update(name, amount, unit, expiryDate, productionDate);
        item.SetTags(ResolveTags(tags));
        PruneUnusedTags();
        return true;
    }

    /// <summary>AC-09: delete an item regardless of amount (SPEC-011 AC-04: its tags may go with it).</summary>
    public void RemoveItem(Guid itemId)
    {
        _items.Remove(GetItem(itemId));
        PruneUnusedTags();
    }

    /// <summary>SPEC-011 AC-05: every tag with the number of items carrying it, sorted by name.</summary>
    public IReadOnlyList<(Tag Tag, int ItemCount)> GetTagsWithCounts() =>
        _tags
            .Select(tag => (tag, _items.Count(item => item.Tags.Contains(tag))))
            .OrderBy(entry => entry.Item1.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// SPEC-011 AC-02/AC-03 (D8): the tags for one item — blanks dropped, duplicates collapsed,
    /// existing tags of the storage reused by normalized name, new ones created with the typed
    /// spelling; more than <see cref="Tag.MaxPerItem"/> distinct tags is a validation error.
    /// </summary>
    private List<Tag> ResolveTags(IEnumerable<string?>? rawTags)
    {
        var resolved = new List<Tag>();
        foreach (var raw in rawTags ?? [])
        {
            var cleaned = Tag.Clean(raw);
            if (cleaned.Length == 0)
            {
                continue;
            }

            var normalized = Tag.Normalize(cleaned);
            if (resolved.Any(tag => tag.NormalizedName == normalized))
            {
                continue;
            }

            var existing = _tags.FirstOrDefault(tag => tag.NormalizedName == normalized);
            if (existing is null)
            {
                existing = new Tag(cleaned);
                _tags.Add(existing);
            }

            resolved.Add(existing);
        }

        if (resolved.Count > Tag.MaxPerItem)
        {
            throw new DomainValidationException(
                "item.tags.tooMany",
                $"An item may carry at most {Tag.MaxPerItem} tags."
            );
        }

        return resolved;
    }

    /// <summary>SPEC-011 AC-04 (D2/D11): a tag no item carries is gone.</summary>
    private void PruneUnusedTags() =>
        _tags.RemoveAll(tag => !_items.Any(item => item.Tags.Contains(tag)));

    /// <summary>
    /// AC-10: items sorted by expiry date ascending; items without expiry date last
    /// (EC-05: those carry only a production date and are never marked expired).
    /// </summary>
    public IReadOnlyList<Item> GetItemsSortedByExpiry() =>
        _items.OrderBy(i => i.ExpiryDate ?? DateOnly.MaxValue).ThenBy(i => i.Name).ToList();

    private Item GetItem(Guid itemId) =>
        _items.FirstOrDefault(i => i.Id == itemId) ?? throw new ItemNotFoundException(itemId);
}

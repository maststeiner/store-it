namespace StoreIt.Domain;

/// <summary>Requested item does not exist in the storage.</summary>
public sealed class ItemNotFoundException(Guid itemId)
    : Exception($"Item '{itemId}' was not found in the storage.")
{
    public Guid ItemId { get; } = itemId;
}

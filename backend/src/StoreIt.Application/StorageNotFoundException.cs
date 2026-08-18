namespace StoreIt.Application;

/// <summary>Requested storage does not exist.</summary>
public sealed class StorageNotFoundException(Guid storageId)
    : Exception($"Storage '{storageId}' was not found.")
{
    public Guid StorageId { get; } = storageId;
}

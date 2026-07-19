using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Api;

// Boundary DTOs (ADR-001: domain entities do not leak through the API).
// Dates are ISO-8601 (DateOnly), enums are locale-neutral codes (arc42 §8 i18n).

public sealed record StorageResponse(Guid Id, string Name, int ItemCount)
{
    public static StorageResponse From(Storage storage) =>
        new(storage.Id, storage.Name, storage.Items.Count);
}

public sealed record ItemResponse(
    Guid Id,
    string Name,
    decimal Amount,
    Unit Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate,
    ExpiryStatus ExpiryStatus
)
{
    public static ItemResponse From(ItemWithStatus itemWithStatus) =>
        new(
            itemWithStatus.Item.Id,
            itemWithStatus.Item.Name,
            itemWithStatus.Item.Amount,
            itemWithStatus.Item.Unit,
            itemWithStatus.Item.ExpiryDate,
            itemWithStatus.Item.ProductionDate,
            itemWithStatus.Status
        );
}

public sealed record StorageRequest(string Name);

public sealed record ItemRequest(
    string Name,
    decimal Amount,
    Unit Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate
);

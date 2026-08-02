using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Api;

// Boundary DTOs (ADR-001: domain entities do not leak through the API).
// Dates are ISO-8601 (DateOnly), enums are locale-neutral codes (arc42 §8 i18n).

public sealed record StorageResponse(
    Guid Id,
    string Name,
    int ItemCount,
    int ExpiredCount,
    int ExpiringSoonCount
)
{
    public static StorageResponse From(StorageSummary summary) =>
        new(
            summary.Id,
            summary.Name,
            summary.ItemCount,
            summary.ExpiredCount,
            summary.ExpiringSoonCount
        );
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
            itemWithStatus.Id,
            itemWithStatus.Name,
            itemWithStatus.Amount,
            itemWithStatus.Unit,
            itemWithStatus.ExpiryDate,
            itemWithStatus.ProductionDate,
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

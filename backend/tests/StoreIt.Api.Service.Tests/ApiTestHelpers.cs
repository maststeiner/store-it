using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace StoreIt.Api.Service.Tests;

public sealed record StorageResponse(
    Guid Id,
    string Name,
    int ItemCount,
    int ExpiredCount,
    int ExpiringSoonCount
);

public sealed record ItemResponse(
    Guid Id,
    string Name,
    decimal Amount,
    string Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate,
    string ExpiryStatus
);

/// <summary>
/// Black-box helpers for the public REST contract (backend/openapi/StoreIt.Api.json).
/// The contract marks expiryDate/productionDate as required nullable properties,
/// so request bodies always carry both keys explicitly.
/// </summary>
internal static class ApiTestHelpers
{
    public static object ItemBody(
        string name = "Milk",
        decimal amount = 1m,
        string unit = "Piece",
        DateOnly? expiryDate = null,
        DateOnly? productionDate = null
    ) =>
        new
        {
            name,
            amount,
            unit,
            expiryDate,
            productionDate,
        };

    public static async Task<StorageResponse> CreateStorageAsync(
        this HttpClient client,
        string name
    )
    {
        var response = await client.PostAsJsonAsync("/api/v1/storages", new { name });
        response.EnsureSuccessStatusCode();
        var storage = await response.Content.ReadFromJsonAsync<StorageResponse>();
        Assert.NotNull(storage);
        return storage;
    }

    public static async Task<Guid> AddItemAsync(
        this HttpClient client,
        Guid storageId,
        object request
    )
    {
        var response = await client.PostAsJsonAsync($"/api/v1/storages/{storageId}/items", request);
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail(
                $"AddItem failed: {(int)response.StatusCode} — {await response.Content.ReadAsStringAsync()}"
            );
        }
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    public static async Task<IReadOnlyList<StorageResponse>> GetStoragesAsync(
        this HttpClient client
    )
    {
        var storages = await client.GetFromJsonAsync<List<StorageResponse>>("/api/v1/storages");
        Assert.NotNull(storages);
        return storages;
    }

    public static async Task<StorageResponse> GetStorageAsync(this HttpClient client, Guid id)
    {
        var storage = await client.GetFromJsonAsync<StorageResponse>($"/api/v1/storages/{id}");
        Assert.NotNull(storage);
        return storage;
    }

    public static async Task<IReadOnlyList<ItemResponse>> GetItemsAsync(
        this HttpClient client,
        Guid storageId
    )
    {
        var items = await client.GetFromJsonAsync<List<ItemResponse>>(
            $"/api/v1/storages/{storageId}/items"
        );
        Assert.NotNull(items);
        return items;
    }

    /// <summary>
    /// Asserts a malformed route id is answered with 400 ProblemDetails and the
    /// locale-neutral <c>request.invalidId</c> code (issue #69) — the same error class
    /// on every endpoint, not the 404 the removed <c>:guid</c> route constraint produced.
    /// The detail names the offending parameter so a client can tell which id was wrong.
    /// </summary>
    public static async Task AssertInvalidRouteIdAsync(
        this HttpResponseMessage response,
        string parameterName
    )
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("request.invalidId", await response.ReadErrorCodeAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(parameterName, document.RootElement.GetProperty("detail").GetString());
    }

    /// <summary>Reads the "errorCode" extension from a ProblemDetails response body.</summary>
    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errorCode").GetString();
    }

    /// <summary>
    /// Asserts an error (ProblemDetails) response body exposes none of the
    /// <c>StorageResponse</c> fields — an unknown id must return 404 without leaking data.
    /// </summary>
    public static async Task AssertNoStorageDataAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (
            var field in new[] { "id", "name", "itemCount", "expiredCount", "expiringSoonCount" }
        )
        {
            Assert.False(
                document.RootElement.TryGetProperty(field, out _),
                $"404 body must not expose storage field '{field}'"
            );
        }
    }
}

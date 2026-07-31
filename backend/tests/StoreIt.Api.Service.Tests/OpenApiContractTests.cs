using System.Text.Json;

namespace StoreIt.Api.Service.Tests;

public sealed class OpenApiContractTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonElement> GetContractAsync()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Contract_ForV1Endpoints_ExposesStableOperationIds()
    {
        var root = await GetContractAsync();
        var paths = root.GetProperty("paths");

        static string OperationId(JsonElement paths, string path, string verb) =>
            paths.GetProperty(path).GetProperty(verb).GetProperty("operationId").GetString()!;

        Assert.Equal("getStorages", OperationId(paths, "/api/v1/storages", "get"));
        Assert.Equal("createStorage", OperationId(paths, "/api/v1/storages", "post"));
        Assert.Equal("renameStorage", OperationId(paths, "/api/v1/storages/{storageId}", "put"));
        Assert.Equal("deleteStorage", OperationId(paths, "/api/v1/storages/{storageId}", "delete"));
        Assert.Equal("getItems", OperationId(paths, "/api/v1/storages/{storageId}/items", "get"));
        Assert.Equal("addItem", OperationId(paths, "/api/v1/storages/{storageId}/items", "post"));
        Assert.Equal(
            "updateItem",
            OperationId(paths, "/api/v1/storages/{storageId}/items/{itemId}", "put")
        );
        Assert.Equal(
            "deleteItem",
            OperationId(paths, "/api/v1/storages/{storageId}/items/{itemId}", "delete")
        );
    }

    [Fact]
    public async Task Contract_ForNumericProperties_UsesPlainTypesWithoutStringUnion()
    {
        var root = await GetContractAsync();
        var schemas = root.GetProperty("components").GetProperty("schemas");

        static JsonElement Property(JsonElement schemas, string schema, string property) =>
            schemas.GetProperty(schema).GetProperty("properties").GetProperty(property);

        static void AssertPlainType(JsonElement property, string expectedType)
        {
            // single scalar type, not a ["<type>","string"] array, and no string pattern
            Assert.Equal(JsonValueKind.String, property.GetProperty("type").ValueKind);
            Assert.Equal(expectedType, property.GetProperty("type").GetString());
            Assert.False(property.TryGetProperty("pattern", out _));
        }

        static void AssertNullableIsoDate(JsonElement property)
        {
            // nullable ISO date: type is the ["string","null"] union, format "date" —
            // the numeric normalisation must leave these string members untouched
            var type = property.GetProperty("type");
            Assert.Equal(JsonValueKind.Array, type.ValueKind);
            bool hasString = false,
                hasNull = false;
            foreach (var member in type.EnumerateArray())
            {
                hasString |= member.GetString() == "string";
                hasNull |= member.GetString() == "null";
            }
            Assert.True(hasString, "date field must allow 'string'");
            Assert.True(hasNull, "date field must allow 'null'");
            Assert.Equal("date", property.GetProperty("format").GetString());
        }

        // every numeric property the transformer targets — request and response
        AssertPlainType(Property(schemas, "ItemResponse", "amount"), "number");
        AssertPlainType(Property(schemas, "ItemRequest", "amount"), "number");
        AssertPlainType(Property(schemas, "StorageResponse", "itemCount"), "integer");
        AssertPlainType(Property(schemas, "StorageResponse", "expiredCount"), "integer");
        AssertPlainType(Property(schemas, "StorageResponse", "expiringSoonCount"), "integer");

        // every public date field stays a nullable ISO date — normalisation must not touch them
        AssertNullableIsoDate(Property(schemas, "ItemResponse", "expiryDate"));
        AssertNullableIsoDate(Property(schemas, "ItemResponse", "productionDate"));
        AssertNullableIsoDate(Property(schemas, "ItemRequest", "expiryDate"));
        AssertNullableIsoDate(Property(schemas, "ItemRequest", "productionDate"));
    }
}

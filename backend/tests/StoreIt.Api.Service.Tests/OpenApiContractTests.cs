using System.Text.Json;
using Microsoft.OpenApi;

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
    public async Task Endpoints_have_stable_operation_ids()
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
        Assert.Equal("updateItem", OperationId(paths, "/api/v1/storages/{storageId}/items/{itemId}", "put"));
        Assert.Equal("deleteItem", OperationId(paths, "/api/v1/storages/{storageId}/items/{itemId}", "delete"));
    }

    [Fact]
    public async Task Numeric_properties_are_plain_numbers_without_string_union()
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

        // every numeric property the transformer targets — request and response
        AssertPlainType(Property(schemas, "ItemResponse", "amount"), "number");
        AssertPlainType(Property(schemas, "ItemRequest", "amount"), "number");
        AssertPlainType(Property(schemas, "StorageResponse", "itemCount"), "integer");
        AssertPlainType(Property(schemas, "StorageResponse", "expiredCount"), "integer");
        AssertPlainType(Property(schemas, "StorageResponse", "expiringSoonCount"), "integer");

        // dates must stay nullable strings — the transformer must not touch them
        Assert.Equal(
            JsonValueKind.Array,
            Property(schemas, "ItemResponse", "expiryDate").GetProperty("type").ValueKind
        );
    }
}

public sealed class NumericSchemaTransformerTests
{
    [Fact]
    public async Task Collapses_number_or_string_to_number_and_drops_pattern()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number | JsonSchemaType.String,
            Pattern = @"^-?(?:0|[1-9]\d*)(?:\.\d+)?$",
            Format = "double",
        };

        await new NumericSchemaTransformer().TransformAsync(schema, null!, CancellationToken.None);

        Assert.Equal(JsonSchemaType.Number, schema.Type);
        Assert.Null(schema.Pattern);
        Assert.Equal("double", schema.Format);
    }

    [Fact]
    public async Task Collapses_integer_or_string_to_integer()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer | JsonSchemaType.String,
            Pattern = @"^-?(?:0|[1-9]\d*)$",
        };

        await new NumericSchemaTransformer().TransformAsync(schema, null!, CancellationToken.None);

        Assert.Equal(JsonSchemaType.Integer, schema.Type);
        Assert.Null(schema.Pattern);
        // Format intentionally not set/asserted — int32 count fields emit no format
    }

    [Fact]
    public async Task Leaves_nullable_string_dates_untouched()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Null | JsonSchemaType.String,
            Format = "date",
        };

        await new NumericSchemaTransformer().TransformAsync(schema, null!, CancellationToken.None);

        Assert.Equal(JsonSchemaType.Null | JsonSchemaType.String, schema.Type);
    }
}

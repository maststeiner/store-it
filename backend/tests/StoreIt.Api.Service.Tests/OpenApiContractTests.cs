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

    [Fact]
    public async Task Contract_ForStringEnums_DeclaresStringType()
    {
        var root = await GetContractAsync();
        var schemas = root.GetProperty("components").GetProperty("schemas");

        static void AssertStringEnum(JsonElement schema, string?[] expectedValues)
        {
            // enums serialise as strings (JsonStringEnumConverter); the contract must
            // say so explicitly so generated clients emit a typed string enum + value list
            Assert.Equal("string", schema.GetProperty("type").GetString());
            var values = new List<string?>();
            foreach (var value in schema.GetProperty("enum").EnumerateArray())
            {
                values.Add(value.GetString());
            }
            Assert.Equal(expectedValues, values);
        }

        // Locale-neutral API codes (SPEC-001, arc42 §8): the API exposes codes; clients
        // translate them to display labels (e.g. Gram -> "g"). Assert the exact code set.
        AssertStringEnum(
            schemas.GetProperty("Unit"),
            ["Piece", "Gram", "Kilogram", "Milliliter", "Liter", "Pack"]
        );
        AssertStringEnum(schemas.GetProperty("ExpiryStatus"), ["Ok", "ExpiringSoon", "Expired"]);
    }

    /// <summary>Every v1 operation that carries a GUID route id (issue #69).</summary>
    private static readonly (string Path, string Verb)[] RouteIdOperations =
    [
        ("/api/v1/storages/{storageId}", "get"),
        ("/api/v1/storages/{storageId}", "put"),
        ("/api/v1/storages/{storageId}", "delete"),
        ("/api/v1/storages/{storageId}/items", "get"),
        ("/api/v1/storages/{storageId}/items", "post"),
        ("/api/v1/storages/{storageId}/items/{itemId}", "put"),
        ("/api/v1/storages/{storageId}/items/{itemId}", "delete"),
    ];

    [Fact]
    public async Task Contract_ForOperationsWithRouteIds_DeclaresBadRequestProblemDetails()
    {
        // #69: a malformed route id answers 400 ProblemDetails API-wide — the contract
        // declares it on every id-carrying operation so generated clients can handle it.
        var root = await GetContractAsync();
        var paths = root.GetProperty("paths");

        foreach (var (path, verb) in RouteIdOperations)
        {
            var schemaReference = paths
                .GetProperty(path)
                .GetProperty(verb)
                .GetProperty("responses")
                .GetProperty("400")
                .GetProperty("content")
                .GetProperty("application/problem+json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString();

            Assert.Equal("#/components/schemas/ProblemDetails", schemaReference);
        }
    }

    [Fact]
    public async Task Contract_ForRouteIdParameters_DeclaresUuidFormat()
    {
        // Ids bind as strings in the handlers (#69) — the published contract still
        // documents them as GUIDs so clients keep generating uuid-typed parameters.
        var root = await GetContractAsync();
        var paths = root.GetProperty("paths");
        var pathParameters = 0;

        foreach (var (path, verb) in RouteIdOperations)
        {
            foreach (
                var parameter in paths
                    .GetProperty(path)
                    .GetProperty(verb)
                    .GetProperty("parameters")
                    .EnumerateArray()
            )
            {
                Assert.Equal("path", parameter.GetProperty("in").GetString());
                var schema = parameter.GetProperty("schema");
                Assert.Equal("string", schema.GetProperty("type").GetString());
                Assert.Equal("uuid", schema.GetProperty("format").GetString());
                pathParameters++;
            }
        }

        // 5 storageId-only operations + 2 operations carrying storageId and itemId
        Assert.Equal(9, pathParameters);
    }
}

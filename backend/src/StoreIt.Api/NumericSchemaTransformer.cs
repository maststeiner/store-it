using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StoreIt.Api;

/// <summary>
/// Web-default JSON options (JsonNumberHandling.AllowReadingFromString) make .NET emit
/// numeric schemas as number|string / integer|string with a string pattern. This collapses
/// them to the primary numeric type so the published contract is clean for every client
/// (SPEC-002 AC-01). Runtime deserialisation is unchanged (AC-02) — only the document.
/// Nullable string members (dates: null|string) and enums (no type) are left as-is.
/// </summary>
internal sealed class NumericSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            schema.Type is { } type
            && type.HasFlag(JsonSchemaType.String)
            && (type.HasFlag(JsonSchemaType.Number) || type.HasFlag(JsonSchemaType.Integer))
        )
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }
}

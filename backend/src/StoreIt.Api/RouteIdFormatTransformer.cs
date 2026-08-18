using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StoreIt.Api;

/// <summary>
/// Route ids bind as strings so a malformed id answers 400 ProblemDetails instead of
/// leaving the route unmatched (issue #69). The published contract must still declare
/// them as GUIDs: generated clients and docs keep the <c>format: uuid</c> they had under
/// the <c>:guid</c> route constraint (ADR-006, SPEC-002) — the declared 400 response
/// documents what a non-GUID value gets. Applies to path parameters named <c>*Id</c>.
/// </summary>
internal sealed class RouteIdFormatTransformer : IOpenApiOperationTransformer
{
    private const string IdParameterSuffix = "Id";
    private const string GuidFormat = "uuid";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        foreach (var parameter in operation.Parameters ?? [])
        {
            if (
                parameter is { In: ParameterLocation.Path, Name: { } name }
                && name.EndsWith(IdParameterSuffix, StringComparison.Ordinal)
                && parameter.Schema is OpenApiSchema { Type: JsonSchemaType.String } schema
            )
            {
                schema.Format = GuidFormat;
            }
        }

        return Task.CompletedTask;
    }
}

using System.Text.Json.Serialization;
using StoreIt.Api;
using StoreIt.Application;
using StoreIt.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 12-factor: config strictly from the environment — no committed fallback.
// The connection string is resolved lazily in AddInfrastructure (required at
// runtime, not during build-time OpenAPI generation).
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi("v1", options => options.AddSchemaTransformer<NumericSchemaTransformer>());

var app = builder.Build();

app.UseExceptionHandler();

app.MapHealthChecks("/health");
app.MapOpenApi();
app.MapStorageEndpointsV1();

await app.RunAsync();

using System.Text.Json.Serialization;
using StoreIt.Api;
using StoreIt.Application;
using StoreIt.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 12-factor: config from the environment (ConnectionStrings__storeit).
// The fallback keeps build-time OpenAPI document generation working.
var connectionString =
    builder.Configuration.GetConnectionString("storeit") ?? "Host=localhost;Database=storeit";

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi("v1");

var app = builder.Build();

app.UseExceptionHandler();

app.MapHealthChecks("/health");
app.MapOpenApi();
app.MapStorageEndpointsV1();

await app.RunAsync();

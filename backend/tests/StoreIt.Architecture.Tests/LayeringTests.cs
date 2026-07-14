using NetArchTest.Rules;

namespace StoreIt.Architecture.Tests;

/// <summary>
/// Enforces the Clean Architecture layering rules from ADR-001.
/// Target: 0 violations (structural debt = 0). Runs as the CI architecture gate.
/// </summary>
public class LayeringTests
{
    private const string ApiNamespace = "StoreIt.Api";
    private const string ApplicationNamespace = "StoreIt.Application";
    private const string DomainNamespace = "StoreIt.Domain";
    private const string InfrastructureNamespace = "StoreIt.Infrastructure";

    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void Domain_MustNotDependOn_AnyOtherLayer()
    {
        var result = Types
            .InAssembly(typeof(Domain.ExpiryRules).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace, ApplicationNamespace, InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void Application_MustNotDependOn_ApiOrInfrastructure()
    {
        var result = Types
            .InAssembly(typeof(Application.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace, InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void Api_MustNotDependOn_Infrastructure()
    {
        var result = Types
            .InAssembly(typeof(Program).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void Infrastructure_MustNotDependOn_Api()
    {
        var result = Types
            .InAssembly(typeof(Infrastructure.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void Domain_MustBe_FrameworkFree()
    {
        // Coding guidelines: Domain is framework-free — plain C#, no EF Core/ASP.NET/etc.
        // Allowed references: the base class library only.
        string[] allowedPrefixes = ["System", "netstandard", "mscorlib"];

        var forbidden = typeof(Domain.ExpiryRules)
            .Assembly.GetReferencedAssemblies()
            .Where(reference =>
                !allowedPrefixes.Any(prefix =>
                    reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true
                )
            )
            .Select(reference => reference.Name)
            .ToList();

        Assert.True(
            forbidden.Count == 0,
            $"Domain must be framework-free but references: {string.Join(", ", forbidden)}"
        );
    }

    private static string FailureMessage(TestResult result)
    {
        var offenders = result.FailingTypes is null
            ? string.Empty
            : string.Join(", ", result.FailingTypes.Select(t => t.FullName));
        return $"Layering violation (ADR-001). Offending types: {offenders}";
    }
}

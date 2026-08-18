using System.Reflection;
using ProjectReferencesRuler;
using ProjectReferencesRuler.Rules;
using ProjectReferencesRuler.Rules.References;

namespace StoreIt.Architecture.Tests;

/// <summary>
/// Enforces ADR-001 at the project/package *reference* level (csproj), complementing
/// the type-level checks in <see cref="LayeringTests"/>: a forbidden reference fails
/// here as soon as it is declared — even before any code uses it.
/// </summary>
public class ReferenceRulesTests
{
    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void ProjectReferences_MustFollow_Adr001Layering()
    {
        var complaints = ProjectsRuler.GetProjectReferencesComplaints(
            GetBackendRootPath(),
            new ReferenceRule(
                patternFrom: "StoreIt.Api",
                patternTo: "StoreIt.Infrastructure",
                RuleKind.Allowed,
                description: "ADR-001 amendment 2026-07-19: composition root (Program) registers Infrastructure for DI — type-level gate still forbids all other Api types"
            ),
            new ReferenceRule(
                patternFrom: "StoreIt.Infrastructure",
                patternTo: "StoreIt.Api",
                RuleKind.Forbidden,
                description: "ADR-001: Infrastructure must not depend on Api"
            ),
            new ReferenceRule(
                patternFrom: "StoreIt.Application",
                patternTo: "StoreIt.Api",
                RuleKind.Forbidden,
                description: "ADR-001: Application must not depend on Api"
            ),
            new ReferenceRule(
                patternFrom: "StoreIt.Application",
                patternTo: "StoreIt.Infrastructure",
                RuleKind.Forbidden,
                description: "ADR-001: Application must not depend on Infrastructure"
            ),
            new ReferenceRule(
                patternFrom: "StoreIt.Domain",
                patternTo: "StoreIt.*",
                RuleKind.Forbidden,
                description: "ADR-001: Domain must not depend on any other backend layer"
            )
        );

        Assert.True(string.IsNullOrEmpty(complaints), complaints);
    }

    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void PackageReferences_InDomain_AreForbidden()
    {
        var complaints = ProjectsRuler.GetPackageReferencesComplaints(
            GetBackendRootPath(),
            new ReferenceRule(
                patternFrom: "StoreIt.Domain",
                patternTo: "*",
                RuleKind.Forbidden,
                description: "Coding guidelines: Domain is framework-free — no package references at all"
            )
        );

        Assert.True(string.IsNullOrEmpty(complaints), complaints);
    }

    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void ProjectReferenceRules_OnViolatingFixture_ProduceComplaints()
    {
        // Negative fixture: proves the rule engine fires — guards against
        // silently ineffective rules (wrong path, wrong pattern).
        var complaints = ProjectsRuler.GetProjectReferencesComplaints(
            GetFixturesPath(),
            new ReferenceRule(
                patternFrom: "Violating.Api",
                patternTo: "Violating.Infrastructure",
                RuleKind.Forbidden,
                description: "Fixture rule: must produce a complaint"
            )
        );

        Assert.False(
            string.IsNullOrEmpty(complaints),
            "Rule engine did not fire on a known project-reference violation."
        );
    }

    [Fact]
    [Trait("Category", "ArchitectureTests")]
    public void PackageReferenceRules_OnViolatingFixture_ProduceComplaints()
    {
        var complaints = ProjectsRuler.GetPackageReferencesComplaints(
            GetFixturesPath(),
            new ReferenceRule(
                patternFrom: "Violating.Domain",
                patternTo: "*",
                RuleKind.Forbidden,
                description: "Fixture rule: must produce a complaint"
            )
        );

        Assert.False(
            string.IsNullOrEmpty(complaints),
            "Rule engine did not fire on a known package-reference violation."
        );
    }

    private static string GetBackendRootPath()
    {
        // bin/<config>/<tfm> → project dir → tests/ → backend/
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        return Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
    }

    private static string GetFixturesPath() =>
        Path.Combine(GetBackendRootPath(), "tests", "StoreIt.Architecture.Tests", "Fixtures");
}

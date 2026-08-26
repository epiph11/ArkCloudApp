using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ArkCloud.Tests.Architecture;

// Sprint 6 fitness functions. Turns the Clean Architecture layering this project has followed
// by convention since Sprint 1 (ArkCloud.sln: Domain / Application / Infrastructure / API) into
// an automated check that fails the build the moment a reference goes the wrong way — instead of
// relying on code review to catch it, or worse, finding out during Sprint 7/8 once Angular and
// microservices are layered on top and the violation is buried under new code.
//
// Loaded once (static, per ArchUnitNET's own perf guidance in its user guide) from the four
// compiled layer assemblies. All four ProjectReferences in the .csproj exist for exactly this —
// ArchUnitNET inspects IL-level dependencies, so the assemblies must actually be built and
// loadable, not just referenced by name.
//
// Layer selection deliberately uses ResideInAssembly(), not ResideInNamespace(): in this
// solution one assembly IS one layer (ArkCloud.Domain.dll, .Application.dll, .Infrastructure.dll,
// .API.dll), so the assembly boundary is the real enforcement boundary — the compiler-level one,
// not a namespace convention someone could accidentally sidestep.
//
// ResideInAssembly() is called with the actual System.Reflection.Assembly object (see the
// DomainAssembly/ApplicationAssembly/InfrastructureAssembly/ApiAssembly fields below), not a name
// string. An earlier version of this file used the string overload (e.g.
// ResideInAssembly("ArkCloud.Domain")) on the assumption it matched the short assembly name —
// it silently matched zero types instead (confirmed with a temporary diagnostic test that dumped
// Architecture.Types.Select(t => t.Assembly.Name), which showed the exact right names, while
// every "Are(Layer)" rule still evaluated as empty). Passing the Assembly object removes the
// ambiguity about what string format the predicate actually expects.
public class ArchitectureTests
{
    // Named once so both the loader and the layer predicates below reference the exact same
    // System.Reflection.Assembly objects — see the note on ResideInAssembly() further down for
    // why that matters.
    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(ArkCloud.Domain.Common.BaseEntity).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ArkCloud.Application.Services.CustomerAppService).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ArkCloud.Infrastructure.Persistence.ArkCloudDbContext).Assembly;
    private static readonly System.Reflection.Assembly ApiAssembly = typeof(Program).Assembly;

    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly)
        .Build();

    // FIX (Sprint 6, found via a real local test run — every single "Are(Layer)"-based rule below
    // was silently matching zero types): ResideInAssembly(string) matches against the assembly's
    // full identity string (name + version + culture + public key token), not the short name a
    // diagnostic dump of Assembly.Name shows you — passing just "ArkCloud.Domain" therefore never
    // matched anything, and every layer predicate was silently empty. ArchUnitNET's own "requires
    // positive evaluation" guard is what surfaced this (it refuses to pass a rule that matched
    // nothing) rather than the rule appearing to succeed vacuously. Fixed by passing the actual
    // loaded System.Reflection.Assembly object instead of a name string — no ambiguity about what
    // string format is expected.
    private readonly IObjectProvider<IType> DomainLayer = Types()
        .That()
        .ResideInAssembly(DomainAssembly)
        .As("ArkCloud.Domain");

    private readonly IObjectProvider<IType> ApplicationLayer = Types()
        .That()
        .ResideInAssembly(ApplicationAssembly)
        .As("ArkCloud.Application");

    private readonly IObjectProvider<IType> InfrastructureLayer = Types()
        .That()
        .ResideInAssembly(InfrastructureAssembly)
        .As("ArkCloud.Infrastructure");

    private readonly IObjectProvider<IType> ApiLayer = Types()
        .That()
        .ResideInAssembly(ApiAssembly)
        .As("ArkCloud.API");

    // Substring match on the full (namespace-qualified) name — deliberately broad rather than
    // pinned to one assembly, since EF Core/ASP.NET Core each ship as several assemblies
    // (Microsoft.EntityFrameworkCore.Relational, Npgsql.EntityFrameworkCore.PostgreSQL,
    // Microsoft.AspNetCore.Mvc.Core, etc.). Scope is "does Domain/Application code directly
    // reference a type whose full name contains this prefix" — catches the real violation (e.g.
    // a Domain entity taking a dependency on DbContext or HttpContext), not every transitive
    // assembly-load edge.
    private readonly IObjectProvider<IType> EntityFrameworkCoreTypes = Types()
        .That()
        .HaveFullNameContaining("Microsoft.EntityFrameworkCore")
        .As("Microsoft.EntityFrameworkCore");

    private readonly IObjectProvider<IType> AspNetCoreTypes = Types()
        .That()
        .HaveFullNameContaining("Microsoft.AspNetCore")
        .As("Microsoft.AspNetCore");

    // --- Dependency direction: Domain <- Application <- Infrastructure <- API. Each layer may
    // only depend on the ones "below" it in this list, never sideways or back up. ---

    [Fact]
    public void Domain_Should_Not_DependOn_Application() =>
        Types()
            .That()
            .Are(DomainLayer)
            .Should()
            .NotDependOnAny(ApplicationLayer)
            .Because("the Domain layer must have zero knowledge of the layers built on top of it")
            .Check(Architecture);

    [Fact]
    public void Domain_Should_Not_DependOn_Infrastructure() =>
        Types()
            .That()
            .Are(DomainLayer)
            .Should()
            .NotDependOnAny(InfrastructureLayer)
            .Because("the Domain layer must have zero knowledge of the layers built on top of it")
            .Check(Architecture);

    [Fact]
    public void Domain_Should_Not_DependOn_Api() =>
        Types()
            .That()
            .Are(DomainLayer)
            .Should()
            .NotDependOnAny(ApiLayer)
            .Because("the Domain layer must have zero knowledge of the layers built on top of it")
            .Check(Architecture);

    [Fact]
    public void Application_Should_Not_DependOn_Infrastructure() =>
        Types()
            .That()
            .Are(ApplicationLayer)
            .Should()
            .NotDependOnAny(InfrastructureLayer)
            .Because(
                "Application depends on abstractions (ArkCloud.Application.Interfaces), "
                    + "Infrastructure provides the concrete implementation — never the other way round"
            )
            .Check(Architecture);

    [Fact]
    public void Application_Should_Not_DependOn_Api() =>
        Types()
            .That()
            .Are(ApplicationLayer)
            .Should()
            .NotDependOnAny(ApiLayer)
            .Because("the Application layer must stay usable outside this specific API host")
            .Check(Architecture);

    [Fact]
    public void Infrastructure_Should_Not_DependOn_Api() =>
        Types()
            .That()
            .Are(InfrastructureLayer)
            .Should()
            .NotDependOnAny(ApiLayer)
            .Because(
                "Infrastructure implements ports for the Application layer, it never needs to know about the host consuming them"
            )
            .Check(Architecture);

    // --- Framework independence: Domain and Application must stay pure C#, no leakage of the
    // persistence framework (EF Core, confined to Infrastructure) or the web framework (ASP.NET
    // Core, confined to API — Application only takes a dependency on
    // Microsoft.Extensions.Logging.Abstractions, which is deliberately framework-agnostic). ---

    [Fact]
    public void Domain_Should_Not_DependOn_EntityFrameworkCore() =>
        Types()
            .That()
            .Are(DomainLayer)
            .Should()
            .NotDependOnAny(EntityFrameworkCoreTypes)
            .Because("the Domain layer must stay persistence-framework-agnostic")
            .Check(Architecture);

    [Fact]
    public void Domain_Should_Not_DependOn_AspNetCore() =>
        Types()
            .That()
            .Are(DomainLayer)
            .Should()
            .NotDependOnAny(AspNetCoreTypes)
            .Because("the Domain layer must stay web-framework-agnostic")
            .Check(Architecture);

    [Fact]
    public void Application_Should_Not_DependOn_EntityFrameworkCore() =>
        Types()
            .That()
            .Are(ApplicationLayer)
            .Should()
            .NotDependOnAny(EntityFrameworkCoreTypes)
            .Because(
                "the Application layer depends on repository interfaces "
                    + "(ArkCloud.Application.Interfaces), never on EF Core directly"
            )
            .Check(Architecture);

    [Fact]
    public void Application_Should_Not_DependOn_AspNetCore() =>
        Types()
            .That()
            .Are(ApplicationLayer)
            .Should()
            .NotDependOnAny(AspNetCoreTypes)
            .Because("the Application layer must stay usable outside an ASP.NET Core host")
            .Check(Architecture);

    // --- Placement conventions: not just "who depends on whom", but "does this kind of type
    // actually live where the layering says it should". Expressed as "Should().Be(Layer)" —
    // every type matching the naming convention must also be a member of the target layer
    // (i.e. compiled into that layer's assembly). ---

    [Fact]
    public void Controllers_Should_Reside_In_Api_Layer()
    {
        var controllers = Classes().That().HaveNameContaining("Controller").As("Controller classes");

        Classes()
            .That()
            .Are(controllers)
            .Should()
            .Be(ApiLayer)
            .Because("only the API layer is allowed to expose HTTP endpoints")
            .Check(Architecture);
    }

    [Fact]
    public void Repository_Implementations_Should_Reside_In_Infrastructure_Layer()
    {
        var repositories = Classes().That().HaveNameContaining("Repository").As("Repository classes");

        Classes()
            .That()
            .Are(repositories)
            .Should()
            .Be(InfrastructureLayer)
            .Because(
                "repository interfaces (I*Repository) live in ArkCloud.Application.Interfaces as "
                    + "ports; only ArkCloud.Infrastructure may contain their concrete adapter"
            )
            .Check(Architecture);
    }
}

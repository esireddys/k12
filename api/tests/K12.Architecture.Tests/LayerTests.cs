using System.Reflection;
using NetArchTest.Rules;

namespace K12.Architecture.Tests;

/// <summary>Fails the build when a layer reaches outward. Dependencies point inward only.</summary>
public class LayerTests
{
    private static readonly Assembly Domain = typeof(K12.Domain.Common.IDomainEvent).Assembly;
    private static readonly Assembly Application = typeof(K12.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(K12.Infrastructure.DependencyInjection).Assembly;

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_depends_on_nothing_outside_itself() =>
        AssertNoDependency(Domain, "K12.Application", "K12.Infrastructure", "K12.Api",
            "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Azure");

    [Fact]
    public void Application_does_not_know_about_infrastructure_or_http() =>
        AssertNoDependency(Application, "K12.Infrastructure", "K12.Api", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore");

    [Fact]
    public void Infrastructure_does_not_know_about_http() =>
        AssertNoDependency(Infrastructure, "K12.Api", "Microsoft.AspNetCore");

    [Fact]
    public void Repository_contracts_live_in_the_domain_as_interfaces()
    {
        var result = Types.InAssembly(Domain).That().HaveNameEndingWith("Repository").Should().BeInterfaces().GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Use_case_handlers_are_sealed()
    {
        var result = Types.InAssembly(Application).That().HaveNameEndingWith("Handler").And().AreClasses()
            .Should().BeSealed().GetResult();
        Assert.True(result.IsSuccessful, "Unsealed: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}

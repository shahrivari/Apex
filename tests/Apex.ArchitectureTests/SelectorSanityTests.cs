using NetArchTest.Rules;
using R = Apex.ArchitectureTests.ArchitectureRules;

namespace Apex.ArchitectureTests;

/// <summary>
/// NetArchTest reports success when a selector matches zero types, so a typo in a namespace or name
/// suffix would make a rule pass while checking nothing. These tests assert the selectors used by the
/// real rules actually bind to types, so the suite cannot rot into false green.
/// </summary>
public sealed class SelectorSanityTests
{
    [Fact]
    public void Domain_selector_matches_types()
    {
        var domainTypes = Types.InAssembly(R.Accounting)
            .That().ResideInNamespaceEndingWith(".Domain")
            .GetTypes();

        Assert.NotEmpty(domainTypes);
    }

    [Fact]
    public void Handler_selector_matches_types()
    {
        var handlers = Types.InAssembly(R.Accounting)
            .That().HaveNameEndingWith("Handler")
            .GetTypes();

        Assert.NotEmpty(handlers);
    }

    [Fact]
    public void Endpoint_selector_matches_types()
    {
        var endpoints = Types.InAssembly(R.Accounting)
            .That().HaveNameEndingWith("Endpoint")
            .GetTypes();

        Assert.NotEmpty(endpoints);
    }
}

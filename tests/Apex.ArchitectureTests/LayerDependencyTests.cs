using NetArchTest.Rules;
using R = Apex.ArchitectureTests.ArchitectureRules;

namespace Apex.ArchitectureTests;

/// <summary>
/// Enforces the mandatory dependency direction from architecture_guide.md §2:
/// <code>
/// Apex.Api → Apex.Infrastructure, Apex.Modules.*
/// Apex.Modules.* → Apex.Application
/// Apex.Infrastructure → Apex.Application
/// </code>
/// and the cross-module isolation of §3 / §13.3.
/// </summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void Application_should_not_depend_on_Api_Infrastructure_or_modules()
    {
        var result = Types.InAssembly(R.Application)
            .That().ResideInNamespaceStartingWith(R.ApplicationNamespace)
            .ShouldNot().HaveDependencyOnAny(R.ApiNamespace, R.InfrastructureNamespace, R.ModulesNamespace)
            .GetResult();

        R.AssertPasses(result);
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api_or_modules()
    {
        var result = Types.InAssembly(R.Infrastructure)
            .That().ResideInNamespaceStartingWith(R.InfrastructureNamespace)
            .ShouldNot().HaveDependencyOnAny(R.ApiNamespace, R.ModulesNamespace)
            .GetResult();

        R.AssertPasses(result);
    }

    [Fact]
    public void Modules_should_not_depend_on_Api_or_Infrastructure()
    {
        // Modules may depend only on Apex.Application; the composition root wires infrastructure in.
        var result = Types.InAssembly(R.Accounting)
            .That().ResideInNamespaceStartingWith(R.ModulesNamespace)
            .ShouldNot().HaveDependencyOnAny(R.ApiNamespace, R.InfrastructureNamespace)
            .GetResult();

        R.AssertPasses(result);
    }

    [Fact]
    public void Modules_should_not_depend_on_other_modules()
    {
        // §13.3: a module must never reference another module's internals. With one module today this
        // guards nothing yet; when a second module is added it references the others' assemblies here
        // and the loop enforces isolation. A module referencing another module's public Contracts
        // namespace would need that namespace explicitly allow-listed.
        foreach (var module in R.ModuleNamespaces)
        {
            var otherModules = R.ModuleNamespaces.Where(other => other != module).ToArray();
            if (otherModules.Length == 0)
                continue;

            var result = Types.InAssembly(R.Accounting)
                .That().ResideInNamespaceStartingWith(module)
                .ShouldNot().HaveDependencyOnAny(otherModules)
                .GetResult();

            R.AssertPasses(result);
        }
    }
}

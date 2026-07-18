using System.Reflection;
using NetArchTest.Rules;

namespace Apex.ArchitectureTests;

/// <summary>
/// Shared references for architecture tests: the assemblies under inspection and the namespace /
/// dependency constants the guide's rules are expressed against. See <c>docs/architecture_guide.md</c>.
/// </summary>
internal static class ArchitectureRules
{
    // One marker type per assembly so NetArchTest inspects the compiled IL.
    internal static readonly Assembly Application =
        typeof(Apex.Application.Abstractions.Time.IClock).Assembly;

    internal static readonly Assembly Infrastructure =
        typeof(Apex.Infrastructure.DependencyInjection).Assembly;

    internal static readonly Assembly Accounting =
        typeof(Apex.Modules.Accounting.AccountingModule).Assembly;

    // Root namespaces used both to select types and to forbid dependencies.
    internal const string ApiNamespace = "Apex.Api";
    internal const string ApplicationNamespace = "Apex.Application";
    internal const string InfrastructureNamespace = "Apex.Infrastructure";
    internal const string ModulesNamespace = "Apex.Modules";

    // Technical concerns the write/read/domain layers must not leak into the wrong place.
    internal const string Dapper = "Dapper";
    internal const string SqlClient = "Microsoft.Data.SqlClient";
    internal const string AspNetCore = "Microsoft.AspNetCore";
    internal const string FluentValidation = "FluentValidation";
    internal const string Mapster = "Mapster";

    /// <summary>
    /// Every module lives under this namespace prefix. Extend the cross-module tests when a second
    /// module is added (see <see cref="LayerDependencyTests"/>).
    /// </summary>
    internal static readonly IReadOnlyList<string> ModuleNamespaces =
    [
        "Apex.Modules.Accounting"
    ];

    internal static void AssertPasses(TestResult result)
    {
        var failing = result.FailingTypeNames ?? [];
        Assert.True(
            result.IsSuccessful,
            "Architecture rule violated by:" + Environment.NewLine +
            string.Join(Environment.NewLine, failing.Select(name => "  - " + name)));
    }
}

using NetArchTest.Rules;
using R = Apex.ArchitectureTests.ArchitectureRules;

namespace Apex.ArchitectureTests;

/// <summary>
/// Enforces the domain-purity rules of architecture_guide.md §8 and the persistence-placement rules
/// of §10/§11: domain entities stay free of frameworks, and SQL/Dapper stays out of handlers.
/// </summary>
public sealed class DomainAndPersistenceTests
{
    [Fact]
    public void Domain_types_should_not_depend_on_persistence_or_web_frameworks()
    {
        // §8: "Domain entities MUST NOT reference ASP.NET Core, Dapper, SQL connections, ...".
        var result = Types.InAssembly(R.Accounting)
            .That().ResideInNamespaceEndingWith(".Domain")
            .ShouldNot().HaveDependencyOnAny(R.Dapper, R.SqlClient, R.AspNetCore, R.FluentValidation, R.Mapster)
            .GetResult();

        R.AssertPasses(result);
    }

    [Fact]
    public void Handlers_should_not_depend_on_Dapper_or_SqlClient()
    {
        // §4/§10: a handler orchestrates through repositories; it MUST NOT contain raw SQL. It cannot
        // reference the SQL mapper directly without breaking that rule.
        var result = Types.InAssembly(R.Accounting)
            .That().HaveNameEndingWith("Handler")
            .ShouldNot().HaveDependencyOnAny(R.Dapper, R.SqlClient)
            .GetResult();

        R.AssertPasses(result);
    }

    [Fact]
    public void Endpoints_should_not_depend_on_Dapper_or_SqlClient()
    {
        // §5: use-case endpoint files contain HTTP concerns only — no database access.
        var result = Types.InAssembly(R.Accounting)
            .That().HaveNameEndingWith("Endpoint")
            .ShouldNot().HaveDependencyOnAny(R.Dapper, R.SqlClient)
            .GetResult();

        R.AssertPasses(result);
    }
}

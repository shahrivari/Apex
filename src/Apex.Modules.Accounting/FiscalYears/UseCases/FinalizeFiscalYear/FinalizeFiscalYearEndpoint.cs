using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;

public static class FinalizeFiscalYearEndpoint
{
    public static RouteGroupBuilder MapFinalizeFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:long}/finalize", (long id, [FromBody] FinalizeFiscalYearRequest request) =>
            RejectDirectFinalization())
            .WithName("FinalizeFiscalYear");
        return group;
    }

    private static IResult RejectDirectFinalization() =>
        throw new ConflictException(
            "Fiscal year finalization is unavailable until coordinated journal finalization is implemented.",
            FiscalYearErrors.CannotBeFinalized);
}

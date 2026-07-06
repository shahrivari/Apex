using Apex.Api.Extensions;
using Apex.Application.Abstractions.Exceptions;
using Apex.Infrastructure;
using Apex.Modules.Accounting;
using Apex.Modules.Accounting.Endpoints;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAccountingModule(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseGlobalExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();


app.MapGet("/debug/errors/not-found", () =>
{
    throw new NotFoundException("Debug entity was not found.", "debug_not_found");
})
.AllowAnonymous()
.WithTags("Debug");

app.MapGet("/debug/errors/conflict", () =>
{
    throw new ConflictException("Debug conflict.", "debug_conflict");
})
.AllowAnonymous()
.WithTags("Debug");

app.MapGet("/debug/errors/business-rule", () =>
{
    throw new BusinessRuleException("Debug business rule violation.", "debug_business_rule");
})
.AllowAnonymous()
.WithTags("Debug");

app.MapGet("/debug/errors/unexpected", () =>
{
    throw new InvalidOperationException("Debug unexpected exception.");
})
.AllowAnonymous()
.WithTags("Debug");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .AllowAnonymous()
    .WithTags("System");

app.MapAccountingEndpoints();

app.Run();

public partial class Program;

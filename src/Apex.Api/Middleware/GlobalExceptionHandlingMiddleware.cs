namespace Apex.Api.Middleware;

using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Data;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NanoidDotNet;

public sealed class GlobalExceptionHandlingMiddleware
{
    private const string ErrorTypeBaseUrl = "https://errors.apex.local/";

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Nanoid.Generate(size: 12);
        context.Items["TraceId"] = traceId;

        try
        {
            await _next(context);
        }
        catch (ValidationException exception)
        {
            await HandleValidationExceptionAsync(context, exception, traceId);
        }
        catch (NotFoundException exception)
        {
            await HandleApexExceptionAsync(
                context,
                exception,
                traceId,
                StatusCodes.Status404NotFound,
                "Not found",
                LogLevel.Warning);
        }
        catch (ConflictException exception)
        {
            await HandleApexExceptionAsync(
                context,
                exception,
                traceId,
                StatusCodes.Status409Conflict,
                "Conflict",
                LogLevel.Warning);
        }
        catch (BusinessRuleException exception)
        {
            await HandleApexExceptionAsync(
                context,
                exception,
                traceId,
                StatusCodes.Status422UnprocessableEntity,
                "Business rule violation",
                LogLevel.Warning);
        }
        catch (ForbiddenException exception)
        {
            await HandleApexExceptionAsync(
                context,
                exception,
                traceId,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                LogLevel.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            await HandleUnauthorizedExceptionAsync(context, exception, traceId);
        }
        catch (ShardAssignmentNotFoundException exception)
        {
            await HandleShardExceptionAsync(context, exception, traceId, "shard_assignment_not_found");
        }
        catch (ShardSchemaMismatchException exception)
        {
            await HandleShardExceptionAsync(context, exception, traceId, "shard_schema_mismatch");
        }
        catch (ShardUnavailableException exception)
        {
            await HandleShardExceptionAsync(context, exception, traceId, "shard_unavailable");
        }
        catch (Exception exception)
        {
            await HandleUnexpectedExceptionAsync(context, exception, traceId);
        }
    }

    private async Task HandleValidationExceptionAsync(
        HttpContext context,
        ValidationException exception,
        string traceId)
    {
        Log(context, exception, LogLevel.Warning, "Validation failed.", traceId);

        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        var problemDetails = CreateProblemDetails(
            context,
            StatusCodes.Status400BadRequest,
            "Validation failed",
            "One or more validation errors occurred.",
            ErrorCodes.ValidationFailed,
            traceId);

        problemDetails.Extensions["errors"] = errors;

        await WriteProblemDetailsAsync(context, problemDetails);
    }

    private async Task HandleApexExceptionAsync(
        HttpContext context,
        ApexException exception,
        string traceId,
        int statusCode,
        string title,
        LogLevel logLevel)
    {
        Log(context, exception, logLevel, exception.Message, traceId);

        var problemDetails = CreateProblemDetails(
            context,
            statusCode,
            title,
            exception.Message,
            exception.ErrorCode,
            traceId);

        await WriteProblemDetailsAsync(context, problemDetails);
    }

    private async Task HandleUnauthorizedExceptionAsync(
        HttpContext context,
        UnauthorizedAccessException exception,
        string traceId)
    {
        Log(context, exception, LogLevel.Warning, exception.Message, traceId);

        var problemDetails = CreateProblemDetails(
            context,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            exception.Message,
            "unauthorized",
            traceId);

        await WriteProblemDetailsAsync(context, problemDetails);
    }

    private async Task HandleUnexpectedExceptionAsync(
        HttpContext context,
        Exception exception,
        string traceId)
    {
        Log(context, exception, LogLevel.Error, "Unexpected error.", traceId);

        var problemDetails = CreateProblemDetails(
            context,
            StatusCodes.Status500InternalServerError,
            "Unexpected error",
            "An unexpected error occurred.",
            ErrorCodes.UnexpectedError,
            traceId);

        await WriteProblemDetailsAsync(context, problemDetails);
    }

    private async Task HandleShardExceptionAsync(
        HttpContext context,
        ShardResolutionException exception,
        string traceId,
        string errorCode)
    {
        Log(context, exception, LogLevel.Warning, exception.Message, traceId);
        var problemDetails = CreateProblemDetails(
            context,
            StatusCodes.Status503ServiceUnavailable,
            "Shard unavailable",
            "The requested accounting partition is temporarily unavailable.",
            errorCode,
            traceId);
        await WriteProblemDetailsAsync(context, problemDetails);
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode,
        string traceId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = ErrorTypeBaseUrl + errorCode,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["errorCode"] = errorCode;

        return problemDetails;
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        ProblemDetails problemDetails)
    {
        context.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json");
    }

    private void Log(
        HttpContext context,
        Exception exception,
        LogLevel logLevel,
        string message,
        string traceId)
    {
        _logger.Log(
            logLevel,
            exception,
            "{Message} TraceId={TraceId} Path={Path}",
            message,
            traceId,
            context.Request.Path);
    }
}

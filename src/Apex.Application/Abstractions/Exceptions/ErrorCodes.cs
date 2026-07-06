namespace Apex.Application.Abstractions.Exceptions;

public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string UnexpectedError = "unexpected_error";

    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string Forbidden = "forbidden";
    public const string BusinessRuleViolation = "business_rule_violation";
}

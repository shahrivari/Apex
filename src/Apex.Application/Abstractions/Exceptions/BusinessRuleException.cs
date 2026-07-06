namespace Apex.Application.Abstractions.Exceptions;

public sealed class BusinessRuleException : ApexException
{
    public BusinessRuleException(
        string message,
        string errorCode = "business_rule_violation")
        : base(message, errorCode)
    {
    }
}

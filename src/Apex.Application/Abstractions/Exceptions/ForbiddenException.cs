namespace Apex.Application.Abstractions.Exceptions;

public sealed class ForbiddenException : ApexException
{
    public ForbiddenException(
        string message,
        string errorCode = "forbidden")
        : base(message, errorCode)
    {
    }
}

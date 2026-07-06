namespace Apex.Application.Abstractions.Exceptions;

public sealed class ConflictException : ApexException
{
    public ConflictException(
        string message,
        string errorCode = "conflict")
        : base(message, errorCode)
    {
    }
}

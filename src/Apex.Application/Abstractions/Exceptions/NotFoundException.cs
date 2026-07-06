namespace Apex.Application.Abstractions.Exceptions;

public sealed class NotFoundException : ApexException
{
    public NotFoundException(
        string message,
        string errorCode = "not_found")
        : base(message, errorCode)
    {
    }
}

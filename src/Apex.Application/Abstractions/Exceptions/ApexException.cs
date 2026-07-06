namespace Apex.Application.Abstractions.Exceptions;

public abstract class ApexException : Exception
{
    protected ApexException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

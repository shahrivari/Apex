namespace Apex.Application.Abstractions.Authentication;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    long? UserId { get; }
    string? Username { get; }
}

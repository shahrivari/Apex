namespace Apex.Infrastructure.Time;

using Apex.Application.Abstractions.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

namespace Apex.Infrastructure.Resilience;

using Polly;
using Polly.Retry;

public static class SqlResiliencePolicy
{
    public static AsyncRetryPolicy CreateDefaultRetryPolicy()
    {
        return Policy
            .Handle<Microsoft.Data.SqlClient.SqlException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * attempt));
    }
}

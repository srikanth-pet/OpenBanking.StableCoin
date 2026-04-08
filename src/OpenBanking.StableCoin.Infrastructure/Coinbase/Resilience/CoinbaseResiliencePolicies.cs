using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace OpenBanking.StableCoin.Infrastructure.Coinbase.Resilience;

public static class CoinbaseResiliencePolicies
{
    public static Action<HttpStandardResilienceOptions> Configure() => opts =>
    {
        // Total timeout across all retries: 30 seconds
        opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);

        // Per-attempt timeout: 10 seconds
        opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

        // Retry: 3 attempts, exponential backoff 2s/4s/8s
        // Does NOT retry 4xx (except 429)
        opts.Retry.MaxRetryAttempts = 3;
        opts.Retry.Delay = TimeSpan.FromSeconds(2);
        opts.Retry.BackoffType = DelayBackoffType.Exponential;
        opts.Retry.UseJitter = true;
        opts.Retry.ShouldHandle = args =>
        {
            // Retry on network errors, 5xx, and 429
            if (args.Outcome.Exception != null) return ValueTask.FromResult(true);
            var status = args.Outcome.Result?.StatusCode;
            return ValueTask.FromResult(
                status == HttpStatusCode.TooManyRequests ||
                (status.HasValue && (int)status.Value >= 500));
        };

        // Circuit breaker: open after 5 failures in 30 second sampling window
        opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        opts.CircuitBreaker.FailureRatio = 0.5;
        opts.CircuitBreaker.MinimumThroughput = 5;
        opts.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    };
}

using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Catalog.GrpcClient.Resilience;

public class GrpcResiliencePolicy : IGrpcResiliencePolicy
{
    private readonly ILogger<GrpcResiliencePolicy> _logger;
    private readonly IAsyncPolicy _policy;

    public GrpcResiliencePolicy(ILogger<GrpcResiliencePolicy> logger)
    {
        _logger = logger;

        var random = new Random();

        // Retry com backoff exponencial + jitter
        AsyncRetryPolicy retryPolicy = Policy
            .Handle<RpcException>(IsTransient)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                {
                    var baseDelayMs = 200 * Math.Pow(2, attempt - 1); 
                    var jitterMs = random.Next(0, 100);               
                    return TimeSpan.FromMilliseconds(baseDelayMs + jitterMs);
                },
                onRetry: (ex, delay, attempt, ctx) =>
                {
                    _logger.LogWarning(ex,
                        "[Resiliência] Tentativa {Attempt} falhou com {Status}. Novo retry em {Delay}...",
                        attempt,
                        (ex as RpcException)?.StatusCode,
                        delay);
                });

        // Circuit breaker: se falhar 5 vezes seguidas, abre por 30 segundos
        AsyncCircuitBreakerPolicy circuitBreakerPolicy = Policy
            .Handle<RpcException>(IsTransient)
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, breakDelay) =>
                {
                    _logger.LogWarning(ex,
                        "[Resiliência] Circuit breaker ABERTO por {Delay}. Motivo: {Message}",
                        breakDelay,
                        ex.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("[Resiliência] Circuit breaker FECHADO novamente.");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("[Resiliência] Circuit breaker HALF-OPEN. Testando requisições...");
                });

        _policy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    private static bool IsTransient(RpcException ex) =>
        ex.StatusCode == StatusCode.Unavailable ||
        ex.StatusCode == StatusCode.DeadlineExceeded;

    public Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => action(ct), cancellationToken);
}

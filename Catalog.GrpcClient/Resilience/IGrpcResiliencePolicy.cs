using System;
using System.Threading;
using System.Threading.Tasks;

namespace Catalog.GrpcClient.Resilience;

public interface IGrpcResiliencePolicy
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}

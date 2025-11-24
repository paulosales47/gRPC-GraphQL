using Catalog.GrpcApi;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Catalog.GrpcClient;

public class CatalogGrpcClient
{
    private readonly ProductService.ProductServiceClient _client;
    private readonly ILogger<CatalogGrpcClient> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public CatalogGrpcClient(ProductService.ProductServiceClient client, ILogger<CatalogGrpcClient> logger)
    {
        _client = client;
        _logger = logger;

        // Política de retry com Polly:
        // - Só tenta novamente em erros transitórios (Unavailable, DeadlineExceeded)
        // - 3 tentativas
        // - Backoff exponencial simples (200ms, 400ms, 800ms)
        _retryPolicy = Policy
            .Handle<RpcException>(ex =>
                ex.StatusCode == StatusCode.Unavailable ||
                ex.StatusCode == StatusCode.DeadlineExceeded)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)),
                onRetry: (ex, delay, attempt, ctx) =>
                {
                    _logger.LogWarning(ex,
                        "Tentativa {Attempt} falhou com {Status}. Novo retry em {Delay}...",
                        attempt,
                        (ex as RpcException)?.StatusCode,
                        delay);
                });
    }

    // ==== Helpers comuns =====================================================

    private Metadata BuildMetadata()
        => new()
        {
            { "x-correlation-id", Guid.NewGuid().ToString() },
            { "authorization", "Bearer 123" }
        };

    private DateTime GetDeadline() => DateTime.UtcNow.AddSeconds(5);

    private Task<T> ExecuteWithRetry<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
        => _retryPolicy.ExecuteAsync(ct => action(ct), cancellationToken);

    // ==== Operações de CRUD expostas como métodos públicos ===================

    public Task<ProductResponse> CreateProductAsync(
        string name,
        ulong priceCents,
        uint stock,
        CancellationToken cancellationToken = default)
        => ExecuteWithRetry(async ct =>
        {
            var call = _client.CreateProductAsync(
                new CreateProductRequest
                {
                    Name = name,
                    PriceCents = priceCents,
                    Stock = stock
                },
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            var response = await call.ResponseAsync;
            _logger.LogInformation("CreateProductAsync -> {Id} - {Name}", response.Id, response.Name);
            return response;
        }, cancellationToken);

    public Task<ProductResponse> GetProductAsync(
        int id,
        CancellationToken cancellationToken = default)
        => ExecuteWithRetry(async ct =>
        {
            var call = _client.GetProductAsync(
                new GetProductRequest { Id = id },
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            var response = await call.ResponseAsync;
            _logger.LogInformation("GetProductAsync -> {Id} - {Name}", response.Id, response.Name);
            return response;
        }, cancellationToken);

    public Task<ListProductsResponse> ListProductsAsync(
        CancellationToken cancellationToken = default)
        => ExecuteWithRetry(async ct =>
        {
            var call = _client.ListProductsAsync(
                new ListProductsRequest(),
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            var response = await call.ResponseAsync;

            _logger.LogInformation("ListProductsAsync -> {Count} produto(s).", response.Products.Count);
            foreach (var p in response.Products)
            {
                _logger.LogInformation(" - {Id}: {Name} ({PriceCents} cents, stock {Stock})",
                    p.Id, p.Name, p.PriceCents, p.Stock);
            }

            return response;
        }, cancellationToken);

    // Patch parcial: atualiza apenas o nome usando FieldMask
    public Task<ProductResponse> PatchProductNameAsync(
        int id,
        string newName,
        CancellationToken cancellationToken = default)
        => ExecuteWithRetry(async ct =>
        {
            var call = _client.PatchProductAsync(
                new PatchProductRequest
                {
                    Id = id,
                    Name = newName,
                    UpdateMask = new FieldMask
                    {
                        Paths = { "name" } // só aplica o campo "name"
                    }
                },
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            var response = await call.ResponseAsync;
            _logger.LogInformation("PatchProductNameAsync -> {Id} -> {Name}", response.Id, response.Name);
            return response;
        }, cancellationToken);

    public Task DeleteProductAsync(
        int id,
        CancellationToken cancellationToken = default)
        => ExecuteWithRetry(async ct =>
        {
            var call = _client.DeleteProductAsync(
                new DeleteProductRequest { Id = id },
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            await call.ResponseAsync;
            _logger.LogInformation("DeleteProductAsync -> {Id} deletado.", id);
            return 0; // o tipo genérico precisa de um retorno, então usamos um int "dummy"
        }, cancellationToken);

    // ==== Orquestrador de demo ===============================================

    public async Task RunDemoAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando demo de CRUD via gRPC Client...");

        // CREATE
        var created = await CreateProductAsync(
            name: "Produto via gRPC Client",
            priceCents: 2999,
            stock: 10,
            cancellationToken);

        // GET
        var fetched = await GetProductAsync(created.Id, cancellationToken);

        // LIST
        await ListProductsAsync(cancellationToken);

        // PATCH (só o name)
        var patched = await PatchProductNameAsync(
            id: created.Id,
            newName: "Produto atualizado via PATCH",
            cancellationToken);

        // DELETE
        await DeleteProductAsync(patched.Id, cancellationToken);

        _logger.LogInformation("Demo concluída com sucesso.");
    }
}

using Catalog.GrpcApi;
using Catalog.GrpcClient.Resilience;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Catalog.GrpcClient;

public class CatalogGrpcClientV2
{
    private readonly ProductServiceV2.ProductServiceV2Client _client;
    private readonly ILogger<CatalogGrpcClientV2> _logger;
    private readonly IGrpcResiliencePolicy _resiliencePolicy;

    public CatalogGrpcClientV2(ProductServiceV2.ProductServiceV2Client client, ILogger<CatalogGrpcClientV2> logger, IGrpcResiliencePolicy resiliencePolicy)
    {
        _client = client;
        _logger = logger;
        _resiliencePolicy = resiliencePolicy;
    }

    // ==== Helpers comuns =====================================================

    private Metadata BuildMetadata()
        => new()
        {
            { "x-correlation-id", Guid.NewGuid().ToString() },
            { "authorization", "Bearer 123" }
        };

    private DateTime GetDeadline() => DateTime.UtcNow.AddSeconds(5);

    private Task<T> ExecuteWithResilience<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
        => _resiliencePolicy.ExecuteAsync(ct => action(ct), cancellationToken);

    // ==== CRUD (V2 usa GUID como string) =====================================

    public Task<ProductV2Response> CreateProductAsync(
        string name,
        ulong priceCents,
        uint stock,
        CancellationToken cancellationToken = default)
        => ExecuteWithResilience(async ct =>
        {
            var call = _client.CreateProductAsync(
                new CreateProductV2Request
                {
                    Name = name,
                    PriceCents = priceCents,
                    Stock = stock
                },
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            var response = await call.ResponseAsync;
            _logger.LogInformation("CreateProductAsync(V2) -> {Id} - {Name}", response.Id, response.Name);
            return response;
        }, cancellationToken);

    public Task<ProductV2Response> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => ExecuteWithResilience(async ct =>
        {
            var call = _client.GetProductAsync(
                new GetProductV2Request { Id = id.ToString() },
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            var response = await call.ResponseAsync;
            _logger.LogInformation("GetProductAsync(V2) -> {Id} - {Name}", response.Id, response.Name);
            return response;
        }, cancellationToken);

    public Task<ListProductsV2Response> ListProductsAsync(
        CancellationToken cancellationToken = default)
        => ExecuteWithResilience(async ct =>
        {
            var call = _client.ListProductsAsync(
                new ListProductsV2Request(),
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            var response = await call.ResponseAsync;

            _logger.LogInformation("ListProductsAsync(V2) -> {Count} produto(s).", response.Products.Count);
            foreach (var p in response.Products)
            {
                _logger.LogInformation(" - {Id}: {Name} ({PriceCents} cents, stock {Stock})",
                    p.Id, p.Name, p.PriceCents, p.Stock);
            }

            return response;
        }, cancellationToken);

    public Task<ProductV2Response> PatchProductNameAsync(
        Guid id,
        string newName,
        CancellationToken cancellationToken = default)
        => ExecuteWithResilience(async ct =>
        {
            var call = _client.PatchProductAsync(
                new PatchProductV2Request
                {
                    Id = id.ToString(),
                    Name = newName,
                    UpdateMask = new FieldMask
                    {
                        Paths = { "name" } // só o nome será atualizado
                    }
                },
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            var response = await call.ResponseAsync;
            _logger.LogInformation("PatchProductNameAsync(V2) -> {Id} -> {Name}", response.Id, response.Name);
            return response;
        }, cancellationToken);

    public Task DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => ExecuteWithResilience(async ct =>
        {
            var call = _client.DeleteProductAsync(
                new DeleteProductV2Request { Id = id.ToString() },
                headers: BuildMetadata(),
                deadline: GetDeadline(),
                cancellationToken: ct);

            await call.ResponseAsync;
            _logger.LogInformation("DeleteProductAsync(V2) -> {Id} deletado.", id);
            return 0;
        }, cancellationToken);

    // ==== Orquestrador (demo igual ao v1, mas versão 2) =======================

    public async Task RunDemoAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando demo de CRUD via gRPC Client V2...");

        // CREATE
        var created = await CreateProductAsync(
            name: "Produto V2 via gRPC Client",
            priceCents: 0,
            stock: 0,
            cancellationToken);

        // GET
        var fetched = await GetProductAsync(Guid.Parse(created.Id), cancellationToken);

        // LIST
        await ListProductsAsync(cancellationToken);

        // PATCH (só o name)
        var patched = await PatchProductNameAsync(
            id: Guid.Parse(created.Id),
            newName: "Produto V2 atualizado via PATCH",
            cancellationToken);

        // DELETE
        await DeleteProductAsync(Guid.Parse(patched.Id), cancellationToken);

        _logger.LogInformation("Demo V2 concluída com sucesso.");
    }
}

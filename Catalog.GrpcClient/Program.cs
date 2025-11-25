using Catalog.GrpcApi;
using Catalog.GrpcClient;
using Catalog.GrpcClient.Resilience;
using Catalog.GrpcClient.Services.Product.v1;
using Google.Api;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(o =>
    {
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    });
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger("Main");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        // Channel compartilhado
        services.AddSingleton(sp =>
        {
            // ajuste a URL se for diferente
            return GrpcChannel.ForAddress("https://localhost:7073", new GrpcChannelOptions
            {
                HttpHandler = handler,
                LoggerFactory = loggerFactory
            });
        });

        // Clients gRPC gerados
        services.AddSingleton(sp =>
        {
            var channel = sp.GetRequiredService<GrpcChannel>();
            return new ProductServiceV1.ProductServiceV1Client(channel);
        });

        services.AddSingleton(sp =>
        {
            var channel = sp.GetRequiredService<GrpcChannel>();
            return new ProductServiceV2.ProductServiceV2Client(channel);
        });

        // Resiliência compartilhada
        services.AddSingleton<IGrpcResiliencePolicy, GrpcResiliencePolicy>();

        // Seus clients de alto nível
        services.AddSingleton<CatalogGrpcClient>();
        services.AddSingleton<CatalogGrpcClientV2>();
    })
    .Build();

var clientV1 = host.Services.GetRequiredService<CatalogGrpcClient>();
await clientV1.RunDemoAsync();

var clientV2 = host.Services.GetRequiredService<CatalogGrpcClientV2>();
await clientV2.RunDemoAsync();

logger.LogInformation("Demos concluídas. Pressione qualquer tecla para sair...");
Console.ReadKey();
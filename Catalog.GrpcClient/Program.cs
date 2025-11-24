using Catalog.GrpcApi;
using Catalog.GrpcClient;
using Grpc.Net.Client;
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

// Handler para aceitar certificado dev em https://localhost
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};

using var channel = GrpcChannel.ForAddress("https://localhost:7073", new GrpcChannelOptions
{
    HttpHandler = handler,
    LoggerFactory = loggerFactory
});

var grpcClient = new ProductService.ProductServiceClient(channel);
var catalogClient = new CatalogGrpcClient(grpcClient, loggerFactory.CreateLogger<CatalogGrpcClient>());

await catalogClient.RunDemoAsync();

logger.LogInformation("Pressione ENTER para sair.");
Console.ReadLine();
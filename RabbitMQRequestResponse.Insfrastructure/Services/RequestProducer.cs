using Microsoft.Extensions.Hosting;

namespace RabbitMQRequestResponse.Insfrastructure.Services;

public sealed class RequestProducer : BackgroundService
{
    private readonly IRpcClient _rpcClient;

    public RequestProducer(IRpcClient rpcClient)
    {
        _rpcClient = rpcClient;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _rpcClient.StartAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _rpcClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

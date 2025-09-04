using Microsoft.Extensions.Hosting;

namespace RabbitMQRequestResponse.Insfrastructure.Services;

public sealed class RequestConsumer : BackgroundService
{
    private readonly IRpcServer _rpcServer;

    public RequestConsumer(IRpcServer rpcServer)
    {
        _rpcServer = rpcServer;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _rpcServer.StartAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _rpcServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

using Microsoft.Extensions.Hosting;

namespace RabbitMQRequestResponse.Insfrastructure.Services;

public sealed class RequestProducer : BackgroundService
{
    private readonly IRequestSender _requestSender;

    public RequestProducer(IRequestSender requestSender)
    {
        _requestSender = requestSender;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _requestSender.StartAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _requestSender.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

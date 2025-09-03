using RabbitMQ.Client;

namespace RabbitMQRequestResponse.Adapter;

public interface IRequestSender : IAsyncDisposable
{
    Task SendXmlAsync(
        string exchange,
        string routingKey,
        string xml,
        string? correlationId = null,
        IBasicProperties? additionalProperties = null,
        CancellationToken cancellationToken = default);
}
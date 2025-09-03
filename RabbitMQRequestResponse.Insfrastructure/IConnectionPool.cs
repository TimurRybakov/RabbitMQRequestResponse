using RabbitMQ.Client;

namespace RabbitMQRequestResponse.Insfrastructure;

public interface IConnectionPool
{
    Task<(IConnection connection, IChannel channel)> GetChannelAsync(CancellationToken cancellationToken = default);

    void ReturnChannel(IConnection? connection, IChannel? channel);
}
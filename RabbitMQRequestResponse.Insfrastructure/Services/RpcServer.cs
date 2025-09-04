using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQRequestResponse.Insfrastructure.Model;

namespace RabbitMQRequestResponse.Insfrastructure.Services;

public interface IRpcServer : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
}

public sealed class RpcServer : IRpcServer
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _queueName;
    private readonly ILogger<RpcServer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public RpcServer(ILogger<RpcServer> logger, IOptions<RabbitMQOptions> rabbitMQOptions)
    {
        _connectionFactory = new ConnectionFactory
        {
            HostName = rabbitMQOptions.Value.HostName,
            Port = rabbitMQOptions.Value.Port,
            UserName = rabbitMQOptions.Value.UserName,
            Password = rabbitMQOptions.Value.Password
        };
        _queueName = rabbitMQOptions.Value.RequestResponseQueueName;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.QueueDeclareAsync(
                queue: _queueName, durable: false, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
            await _channel.BasicQosAsync(
                prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (object sender, BasicDeliverEventArgs eventArgs) =>
            {
                AsyncEventingBasicConsumer consumer = (AsyncEventingBasicConsumer)sender;
                IChannel channel = consumer.Channel;
                IReadOnlyBasicProperties props = eventArgs.BasicProperties;
                var corellationId = props.CorrelationId;
                string response = string.Empty;
                var replyProps = new BasicProperties
                {
                    CorrelationId = corellationId
                };

                try
                {
                    if (corellationId is null)
                    {
                        response = "CorrelationId is not set.";
                        return;
                    }
                    var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
                    _logger.LogInformation("Message {CorellationId} was processed: {Message}", corellationId, message);
                    response = $"Message {corellationId} processed: {message}.";
                }
                catch (Exception e)
                {
                    _logger.LogError("Error processings message {CorellationId}: {Error}", corellationId, e.Message);
                    response = string.Empty;
                }
                finally
                {
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await channel.BasicPublishAsync(exchange: string.Empty, routingKey: props.ReplyTo!,
                        mandatory: true, basicProperties: replyProps, body: responseBytes);
                    await channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);
                }
            };

            await _channel.BasicConsumeAsync(_queueName, autoAck: false, consumer, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogCritical("Error starting request receiver: {Message}", e.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}

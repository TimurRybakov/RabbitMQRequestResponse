using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQRequestResponse.Insfrastructure.Model;
using System.Text;

namespace RabbitMQRequestResponse.Insfrastructure.Services;

public sealed class RequestConsumer : BackgroundService
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _queueName;
    private readonly ILogger<RequestConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public RequestConsumer(ILogger<RequestConsumer> logger, IOptions<RabbitMQOptions> rabbitMQOptions)
    {        
        _connectionFactory = new ConnectionFactory
        {
            HostName = rabbitMQOptions.Value.HostName,
            UserName = rabbitMQOptions.Value.UserName,
            Password = rabbitMQOptions.Value.Password,
        };
        _queueName = rabbitMQOptions.Value.RequestResponseQueueName;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await StartConsumer(stoppingToken);
        }
        catch (Exception e)
        {
            _logger.LogCritical("Error executing request consumer: {Message}", e.Message);
        }
    }

    public override void Dispose()
    {
        _channel?.CloseAsync().GetAwaiter().GetResult();
        _channel?.Dispose();
        _connection?.CloseAsync().GetAwaiter().GetResult();
        _connection?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task StartConsumer(CancellationToken cancellationToken)
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
                _logger.LogInformation("Message {CorellationId} was processed by {Service}: {Message}",
                    corellationId, Environment.GetEnvironmentVariable("service.name"), message);
                response = $"Message {corellationId} processed.";
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
}

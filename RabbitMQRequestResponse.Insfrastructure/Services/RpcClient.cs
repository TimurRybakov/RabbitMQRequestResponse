using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQRequestResponse.Insfrastructure.Model;
using System.Collections.Concurrent;
using System.Text;

namespace RabbitMQRequestResponse.Insfrastructure.Services;

public interface IRpcClient : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task<string> SendAsync(string message, CancellationToken cancellationToken = default);
}

public sealed class RpcClient : IRpcClient
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _queueName;
    private readonly ILogger<RpcClient> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private string? _responseQueueName;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _callbackMapper = new();

    public RpcClient(ILogger<RpcClient> logger, IOptions<RabbitMQOptions> rabbitMQOptions)
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

            // declare a server-named queue
            QueueDeclareOk queueDeclareResult = await _channel.QueueDeclareAsync();
            _responseQueueName = queueDeclareResult.QueueName;
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += (model, ea) =>
            {
                string? correlationId = ea.BasicProperties.CorrelationId;

                if (false == string.IsNullOrEmpty(correlationId))
                {
                    if (_callbackMapper.TryRemove(correlationId, out var tcs))
                    {
                        var response = Encoding.UTF8.GetString(ea.Body.Span);
                        tcs.TrySetResult(response);
                    }
                }

                return Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(_responseQueueName, autoAck: true, consumer, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogCritical("Error starting request sender: {Message}", e.Message);
        }
    }

    public async Task<string> SendAsync(string message, CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException();
        }

        string correlationId = Guid.NewGuid().ToString();
        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = _responseQueueName
        };

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _callbackMapper.TryAdd(correlationId, tcs);

        var messageBytes = Encoding.UTF8.GetBytes(message);
        await _channel.BasicPublishAsync(
            exchange: string.Empty, routingKey: _queueName, mandatory: true, basicProperties: props, body: messageBytes, cancellationToken);
        _logger.LogInformation("Message {CorellationId} is sent: {Message}.", correlationId, message);

        using CancellationTokenRegistration ctr =
            cancellationToken.Register(() =>
            {
                _callbackMapper.TryRemove(correlationId, out _);
                tcs.SetCanceled();
            });

        return await tcs.Task;
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

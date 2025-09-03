using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;

namespace RabbitMQRequestResponse.Adapter;

public class RequestSender : IRequestSender
{
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private readonly SemaphoreSlim _connLock = new(1, 1);
    private readonly ConcurrentBag<IChannel> _channels = [];
    private readonly int _maxPoolSize;
    private int _currentChannels;

    public RequestSender(IConfiguration configuration)
    {
        if (!int.TryParse(configuration["RequestSender:MaxPoolSize"], out var maxPoolSize))
            throw new NullReferenceException("MaxPoolSize is not set in the configuration");

        _factory = new ConnectionFactory() { ConsumerDispatchConcurrency = (ushort)Environment.ProcessorCount };
        _maxPoolSize = Math.Max(1, maxPoolSize);
    }

    public async Task SendXmlAsync(
        string exchange,
        string routingKey,
        string xml,
        string? correlationId = null,
        IBasicProperties? additionalProperties = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(xml)) throw new ArgumentNullException(nameof(xml));

        var channel = await RentChannelAsync(cancellationToken);
        try
        {
            var body = Encoding.UTF8.GetBytes(xml);
            var props = new BasicProperties
            {
                ContentType = "application/xml",
                DeliveryMode = DeliveryModes.Persistent,
                CorrelationId = correlationId
            };

            // Слияние дополнительных свойств, если переданы
            if (additionalProperties != null)
            {
                // Копируем только часто используемые поля; можно расширить при необходимости
                props.Headers = additionalProperties.Headers ?? props.Headers;
                props.Type = additionalProperties.Type ?? props.Type;
                props.AppId = additionalProperties.AppId ?? props.AppId;
                props.UserId = additionalProperties.UserId ?? props.UserId;
                // и т.д.
            }

            await channel.BasicPublishAsync(
                exchange: exchange ?? string.Empty, routingKey: routingKey ?? string.Empty,
                mandatory: true, basicProperties: props, body, cancellationToken);
        }
        finally
        {
            ReturnChannel(channel);
        }
    }

    private async Task EnsureConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.IsOpen == true) return;

        await _connLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true) return;
            _connection?.Dispose();
            _connection = await _factory.CreateConnectionAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _connLock.Release();
        }
    }

    public async Task<IChannel> RentChannelAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken);

        if (_channels.TryTake(out var ch))
        {
            if (ch.IsOpen) return ch;
            // уронённый канал — освобождаем
            try { ch.Dispose(); } catch { }
            Interlocked.Decrement(ref _currentChannels);
        }

        if (Interlocked.Increment(ref _currentChannels) <= _maxPoolSize)
        {
            try
            {
                var model = await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
                return model;
            }
            catch
            {
                Interlocked.Decrement(ref _currentChannels);
                throw;
            }
        }

        // превысили лимит — ждать доступного
        Interlocked.Decrement(ref _currentChannels);
        // Пассивно ждать пока кто-то вернёт канал
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_channels.TryTake(out var waited))
            {
                if (waited.IsOpen) return waited;
                try { waited.Dispose(); } catch { }
                Interlocked.Decrement(ref _currentChannels);
            }
            await Task.Delay(50, cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    public void ReturnChannel(IChannel channel)
    {
        if (channel == null) return;
        if (!channel.IsOpen)
        {
            try { channel.Dispose(); } catch { }
            Interlocked.Decrement(ref _currentChannels);
            return;
        }
        _channels.Add(channel);
    }

    public async Task StartConsumeAsync(string queue, Func<BasicDeliverEventArgs, Task> onMessageAsync, bool autoAck = false, CancellationToken cancellationToken = default)
    {
        var channel = await RentChannelAsync(cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                await onMessageAsync(ea);
                if (!autoAck)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
            }
            catch
            {
                if (!autoAck)
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            }
        };

        // Запускаем потребление; BasicConsumeAsync возвращает consumerTag
        await channel.BasicConsumeAsync(queue: queue, autoAck: autoAck, consumer: consumer, cancellationToken: cancellationToken);

        // не возвращляем канал в пул, т.к. он занят потреблением
        // при желании можно хранить потребительские каналы отдельно и закрывать при StopConsume
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        // Закрываем все каналы
        while (_channels.TryTake(out var ch))
        {
            try
            {
                await ch.CloseAsync();
                ch.Dispose();
            }
            catch
            {
            }
        }

        try
        {
            if (_connection is not null)
                await _connection.CloseAsync();
            _connection?.Dispose();
        }
        catch { }

        _connLock.Dispose();
        await Task.CompletedTask;
    }
}

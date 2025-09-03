using Extensions.Hosting.AsyncInitialization;
using RabbitMQ.Client;
using System.Collections.Concurrent;
namespace RabbitMQRequestResponse.Insfrastructure;

internal sealed class ChannelPool : IDisposable, IAsyncInitializer
{
    private readonly IConnection _connection;
    private readonly int _startingChannels;
    private readonly ConcurrentBag<IChannel> _channels = [];
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxChannels;

    public ChannelPool(IConnection connection, int startingChannels, int maxChannels)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _startingChannels = startingChannels;
        _maxChannels = maxChannels;
        _semaphore = new SemaphoreSlim(startingChannels, maxChannels);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < _startingChannels; i++)
        {
            await CreateNewChannelAsync(cancellationToken);
        }
    }

    private async ValueTask CreateNewChannelAsync(CancellationToken cancellationToken)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _channels.Add(channel);
    }

    public async Task<IChannel?> TryGetChannelAsync(CancellationToken cancellationToken)
    {
        // Попробуем получить семафор без ожиданий
        if (!await _semaphore.WaitAsync(0))
            return null;

        // Пробуем взять свободный канал
        if (_channels.TryTake(out IChannel? channel))
        {
            return channel;
        }

        // Если все каналы заняты, и можем создать новый, создаем
        if (_channels.Count < _maxChannels)
        {
            channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }

        return channel;
    }

    public void ReturnChannel(IChannel? channel)
    {
        if (channel is not null)
        {
            _channels.Add(channel);
            _semaphore.Release(); // Освобождаем семафор
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (var channel in _channels)
        {
            channel.CloseAsync();
            channel.Dispose();
        }
        _connection.CloseAsync();
        _connection.Dispose();
    }
}
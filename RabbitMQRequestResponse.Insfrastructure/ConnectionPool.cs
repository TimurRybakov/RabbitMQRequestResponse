using Extensions.Hosting.AsyncInitialization;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace RabbitMQRequestResponse.Insfrastructure;

public sealed class ConnectionPool : IConnectionPool, IDisposable, IAsyncInitializer
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly int _maxConnections;
    private readonly int _maxChannelsPerConnection;
    private readonly int _startingConnections;
    private readonly int _startingChannels;
    private readonly ConcurrentBag<IConnection> _connections = [];
    private readonly ConcurrentDictionary<IConnection, ChannelPool> _channelPools;
    private readonly SemaphoreSlim _connectionSemaphore;

    public ConnectionPool(ConnectionPoolOptions options)
    {
        if (options.MaxConnections <= options.StartingConnections || options.StartingConnections < 1 ||
            options.MaxChannelsPerConnection <= options.StartingChannels || options.StartingChannels < 1)
        {
            throw new ArgumentException("Invalid pool configuration.");
        }

        _connectionFactory = new ConnectionFactory() { HostName = options.HostName };
        _maxConnections = options.MaxConnections;
        _maxChannelsPerConnection = options.MaxChannelsPerConnection;
        _startingConnections = options.StartingConnections;
        _startingChannels = options.StartingChannels;

        _channelPools = new ConcurrentDictionary<IConnection, ChannelPool>(concurrencyLevel: -1, capacity: _maxConnections);
        _connectionSemaphore = new SemaphoreSlim(_startingConnections, _maxConnections);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < _startingConnections; i++)
        {
            await CreateNewConnectionAsync(cancellationToken);
        }
    }

    private async Task CreateNewConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        _connections.Add(connection);
        var channelPool = new ChannelPool(connection, _startingChannels, _maxChannelsPerConnection);
        await channelPool.InitializeAsync(cancellationToken);
        _channelPools[connection] = channelPool;
    }

    public async Task<(IConnection connection, IChannel channel)> GetChannelAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (await _connectionSemaphore.WaitAsync(100)) // Попробуем получить семафор в течение короткого времени
            {
                try
                {
                    foreach (var connection in _connections)
                    {
                        var channel = await _channelPools[connection].TryGetChannelAsync(cancellationToken);
                        if (channel is null)
                            continue;

                        return (connection, channel);
                    }

                    // Если все соединения заняты и не превышен лимит на количество соединений
                    if (_connections.Count < _maxConnections)
                    {
                        await CreateNewConnectionAsync(cancellationToken);
                    }
                }
                finally
                {
                    // Если не удалось получить канал, освобождаем семафор
                    _connectionSemaphore.Release();
                }
            }
        }
    }

    public void ReturnChannel(IConnection? connection, IChannel? channel)
    {
        if (channel != null && connection != null)
        {
            _channelPools[connection].ReturnChannel(channel);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (var connection in _connections)
        {
            _channelPools[connection].Dispose();
        }
    }
}

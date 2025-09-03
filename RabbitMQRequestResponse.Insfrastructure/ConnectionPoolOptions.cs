namespace RabbitMQRequestResponse.Insfrastructure;

public sealed record ConnectionPoolOptions(
    string HostName,
    int StartingConnections = 1,
    int MaxConnections = 5,
    int StartingChannels = 3,
    int MaxChannelsPerConnection = 10);

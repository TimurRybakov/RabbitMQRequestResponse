namespace RabbitMQRequestResponse.Insfrastructure.Model;

public sealed record RabbitMQOptions
{
    public string HostName { get; set; } = "";

    public string UserName { get; set; } = "";

    public string Password { get; set; } = "";

    public string RequestResponseQueueName { get; set; } = "";
}
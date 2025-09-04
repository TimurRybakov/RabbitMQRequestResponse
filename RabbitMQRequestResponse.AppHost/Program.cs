var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("username", "test", secret: true);
var password = builder.AddParameter("password", "test", secret: true);
var rabbitmq = builder.AddRabbitMQ("rabbit", username, password, port: 5672)
    .WithManagementPlugin()
    .WithDataVolume(isReadOnly: false);

builder.AddProject<Projects.RabbitMQRequestResponse_Adapter>("adapter")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("rabbit.username", username)
    .WithEnvironment("rabbit.password", password)
    .WithReplicas(2);

builder.AddProject<Projects.RabbitMQRequestResponse_Handler>("handler")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("rabbit.username", username)
    .WithEnvironment("rabbit.password", password)
    .WithReplicas(4);

builder.Build().Run();

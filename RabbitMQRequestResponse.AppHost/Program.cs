var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("username", "test", secret: true);
var password = builder.AddParameter("password", "test", secret: true);
var rabbitmq = builder.AddRabbitMQ("rabbit", username, password, port: 5672)
    .WithManagementPlugin()
    .WithDataVolume(isReadOnly: false);

var adapter1 = builder.AddProject<Projects.RabbitMQRequestResponse_Adapter>("adapter-1")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("rabbit.username", username)
    .WithEnvironment("rabbit.password", password)
    .WithHttpsEndpoint(port: 7301);

var adapter2 = builder.AddProject<Projects.RabbitMQRequestResponse_Adapter>("adapter-2")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("rabbit.username", username)
    .WithEnvironment("rabbit.password", password)
    .WithHttpsEndpoint(port: 7302);

var handler = builder.AddProject<Projects.RabbitMQRequestResponse_Handler>("handler")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("rabbit.username", username)
    .WithEnvironment("rabbit.password", password)
    .WithReplicas(4);

builder.AddProject<Projects.RabbitMQRequestResponse_Gateway>("gateway")
    .WithReference(adapter1)
    .WaitFor(adapter1)
    .WithReference(adapter2)
    .WaitFor(adapter2)
    .WithExternalHttpEndpoints();

builder.Build().Run();

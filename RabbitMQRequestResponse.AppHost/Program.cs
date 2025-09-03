using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("username", "test", secret: true);
var password = builder.AddParameter("password", "test", secret: true);
var rabbitmq = builder.AddRabbitMQ("messaging", username, password).WithManagementPlugin();

builder.AddProject<Projects.RabbitMQRequestResponse_Adapter>("adapter")
       .WithReference(rabbitmq)
       .WithReplicas(2);

builder.Build().Run();

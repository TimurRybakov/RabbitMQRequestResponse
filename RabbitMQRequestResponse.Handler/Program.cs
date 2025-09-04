using RabbitMQRequestResponse.Insfrastructure.Model;
using RabbitMQRequestResponse.Insfrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["RabbitMQ:UserName"] = builder.Configuration.GetValue<string>("rabbit.username");
builder.Configuration["RabbitMQ:Password"] = builder.Configuration.GetValue<string>("rabbit.password");

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<RabbitMQOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddHostedService<RequestConsumer>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();


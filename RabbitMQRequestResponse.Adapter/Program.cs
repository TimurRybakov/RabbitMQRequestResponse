using RabbitMQRequestResponse.Insfrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddConnectionPool(builder.Configuration.GetSection("RabbitMQ:ConnectionPool"));
builder.Services.AddAsyncInitialization();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/weatherforecast", () =>
{
    return Results.Ok();
})
.WithName("GetWeatherForecast")
.WithOpenApi();

await app.InitAndRunAsync();

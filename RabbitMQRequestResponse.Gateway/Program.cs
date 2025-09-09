var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Cofigures the service discovery services
builder.Services.AddServiceDiscovery();

// Add YARP services
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    // Configures a destination resolver that can use service discovery
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapReverseProxy();

app.Run();

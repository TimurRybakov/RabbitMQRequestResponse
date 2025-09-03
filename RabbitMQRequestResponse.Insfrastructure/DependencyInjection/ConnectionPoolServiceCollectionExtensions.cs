using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RabbitMQRequestResponse.Insfrastructure.DependencyInjection;

public static class ConnectionPoolServiceCollectionExtensions
{
    public static IServiceCollection AddConnectionPool(
        this IServiceCollection services, IConfigurationSection poolConsigurationSection)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.Configure<ConnectionPoolOptions>(poolConsigurationSection);
        services.AddSingleton<IConnectionPool, ConnectionPool>(serviceProvider => {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectionPoolOptions>>();
            return new ConnectionPool(options.Value);
        });
        return services;
    }
}

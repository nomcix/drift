using DirectiveDrift.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DirectiveDrift.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddDirectiveDriftPersistence(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory)
    {
        services.AddPooledDbContextFactory<GameDbContext>(
            (serviceProvider, options) => options.UseSqlite(
                connectionStringFactory(serviceProvider)));
        services.AddSingleton<IGameRepository, EfGameRepository>();
        return services;
    }

    public static async Task InitializeDirectiveDriftDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDbContext>>();
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Database.MigrateAsync(cancellationToken);
        await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await database.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

public static class DatabaseExtensions
{
    public static async Task SafeMigrateAsync<TContext>(this IHost host) where TContext : DbContext
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<TContext>>();
        var context = services.GetRequiredService<TContext>();

        try
        {
            // Simple file logging for debugging migration issues
            var logPath = Path.Combine(AppContext.BaseDirectory, $"migration-{typeof(TContext).Name}.log");
            await File.AppendAllTextAsync(logPath, $"{DateTime.UtcNow}: Starting migration for {typeof(TContext).Name}\n");

            logger.LogInformation("Migrating database associated with context {DbContextName}", typeof(TContext).Name);

            // Skip migration for InMemory database
            if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                await File.AppendAllTextAsync(logPath, $"{DateTime.UtcNow}: Skipping InMemory for {typeof(TContext).Name}\n");
                logger.LogInformation("Skipping migration for InMemory database context {DbContextName}", typeof(TContext).Name);
                return;
            }

            var strategy = context.Database.CreateExecutionStrategy();

            // Outer retry loop for database connectivity/readiness
            int maxRetries = 30;
            int delaySeconds = 5;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await strategy.ExecuteAsync(async () =>
                    {
                        await context.Database.MigrateAsync();
                    });
                    
                    await File.AppendAllTextAsync(logPath, $"{DateTime.UtcNow}: Migration successful for {typeof(TContext).Name}\n");
                    logger.LogInformation("Migrated database associated with context {DbContextName}", typeof(TContext).Name);
                    break;
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    await File.AppendAllTextAsync(logPath, $"{DateTime.UtcNow}: Migration attempt {i + 1} failed: {ex.Message}\n");
                    logger.LogWarning(ex, "Database migration attempt {Attempt}/{MaxRetries} failed. Retrying in {Delay} seconds...", 
                        i + 1, maxRetries, delaySeconds);
                    await Task.Delay(delaySeconds * 1000);
                }
            }
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, $"migration-{typeof(TContext).Name}.log");
            await File.AppendAllTextAsync(logPath, $"{DateTime.UtcNow}: Migration fatal error: {ex}\n");
            logger.LogError(ex, "An error occurred while migrating the database used on context {DbContextName}", typeof(TContext).Name);
            throw;
        }
    }
}

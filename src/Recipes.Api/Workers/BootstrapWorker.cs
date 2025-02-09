using Recipes.Api.Data;

namespace Recipes.Api.Workers;

public class BootstrapWorker(IServiceProvider serviceProvider, ILogger<BootstrapWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<Seeder>();
        
        await seeder.ExecuteAsync(stoppingToken);
    }
}
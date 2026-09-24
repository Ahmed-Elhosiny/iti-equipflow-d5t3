using EquipFlow.Application.Budget.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.BackgroundServices;

public sealed class BudgetResetBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<BudgetResetBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessResetsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing budget resets.");
            }

            // Run once every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessResetsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserBudgetRepository>();
        
        var currentDate = DateTimeOffset.UtcNow;
        var budgetsToReset = await repository.GetBudgetsNeedingResetAsync(currentDate, stoppingToken);
        
        var resetCount = 0;
        foreach (var budget in budgetsToReset)
        {
            if (budget.TryReset(currentDate))
            {
                await repository.UpdateAsync(budget, stoppingToken);
                resetCount++;
            }
        }

        if (resetCount > 0)
        {
            logger.LogInformation("Successfully reset {Count} user budgets.", resetCount);
        }
    }
}
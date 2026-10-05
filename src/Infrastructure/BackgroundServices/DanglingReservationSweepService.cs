using EquipFlow.Application.Budget.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.BackgroundServices;

public sealed class DanglingReservationSweepService(
    IServiceScopeFactory scopeFactory,
    ILogger<DanglingReservationSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan ReservationTtl = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepDanglingReservationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while sweeping dangling reservations.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task SweepDanglingReservationsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserBudgetRepository>();
        
        var cutoffDate = DateTimeOffset.UtcNow - ReservationTtl;
        var budgetsWithStaleReservations = await repository.GetBudgetsWithStaleReservationsAsync(cutoffDate, stoppingToken);
        
        var releasedCount = 0;
        foreach (var budget in budgetsWithStaleReservations)
        {
            var staleReservations = budget.Reservations
                .Where(r => r.CreatedAt < cutoffDate.UtcDateTime)
                .ToList();

            foreach (var reservation in staleReservations)
            {
                budget.Release(reservation.Id);
                releasedCount++;
                logger.LogWarning("Force-released dangling reservation {ReservationId} for user {UserId} created at {CreatedAt}.", 
                    reservation.Id, budget.UserId, reservation.CreatedAt);
            }

            if (staleReservations.Any())
            {
                await repository.UpdateAsync(budget, stoppingToken);
            }
        }

        if (releasedCount > 0)
        {
            logger.LogInformation("Successfully force-released {Count} dangling reservations.", releasedCount);
        }
    }
}

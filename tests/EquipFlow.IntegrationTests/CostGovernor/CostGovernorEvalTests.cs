using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Budget.Ports;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using EquipFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;

namespace EquipFlow.IntegrationTests.CostGovernor;

public sealed class CostGovernorEvalTests : IClassFixture<CostGovernorWebApplicationFactory>
{
    private readonly CostGovernorWebApplicationFactory factory;

    public CostGovernorEvalTests(CostGovernorWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task SufficientBudget_ReservesAndCommitsCost()
    {
        var userId = await SeedBudgetAsync(1m);
        var callCount = 0;

        var result = await ExecuteBillableRequestAsync(userId, 1000, "gpt-4o-mini", () => callCount++);

        Assert.Equal(CostGovernorStatus.Reserved, result.Status);
        Assert.Equal(1, callCount);
        await CommitAsync(userId, result.ReservationId!.Value, 0.01m);

        var budget = await ReadBudgetAsync(userId);
        Assert.Equal(0.01m, budget.ConsumedAmount.Amount);
        Assert.Equal(0m, budget.ReservedAmount.Amount);
    }

        [Fact]
    public async Task InsufficientBudget_FallsBackToOllamaAndReservesZeroCost()
    {
        var userId = await SeedBudgetAsync(0.0001m); 
        var callCount = 0;

        // Requesting gpt-4o-mini (0.00054m cost) exceeds 0.0001m budget, cascading to free Ollama
        var result = await ExecuteBillableRequestAsync(userId, 3000, "gpt-4o-mini", () => callCount++);

        Assert.Equal(CostGovernorStatus.Reserved, result.Status);
        Assert.Equal("Ollama", result.ModelName);
        Assert.Equal(0m, result.EstimatedCost);
        Assert.Equal(1, callCount);

        var budget = await ReadBudgetAsync(userId);
        Assert.Equal(0m, budget.ConsumedAmount.Amount);
        Assert.Equal(0m, budget.ReservedAmount.Amount);
    }

    [Fact]
    public async Task FallbackRouting_UsesCheaperModelWhenPrimaryExceedsBudget()
    {
        var userId = await SeedBudgetAsync(0.005m); 
        var callCount = 0;

        // Requesting gpt-4o (0.009m cost) exceeds 0.005m budget, cascading to gpt-4o-mini
        var result = await ExecuteBillableRequestAsync(userId, 3000, "gpt-4o", () => callCount++);

        Assert.Equal(CostGovernorStatus.Reserved, result.Status);
        Assert.Equal("gpt-4o-mini", result.ModelName); 
        Assert.Equal(0.00054m, result.EstimatedCost); 
        Assert.Equal(1, callCount);
    }


    [Fact]
    public async Task ConcurrentRequests_ReserveOnlyWithinAvailableBudget()
    {
        var userId = await SeedBudgetAsync(0.0015m); 
        var callCount = 0;
        
        // Requesting gpt-4o-mini (0.00054m cost). Budget is 0.0015m.
        // Exactly 2 requests will fit in the budget. 
        // The remaining 3 will fail the primary check and cascade to the free Ollama model.
        var requests = Enumerable.Range(0, 5)
            .Select(_ => ExecuteBillableRequestAsync(userId, 3000, "gpt-4o-mini", () => Interlocked.Increment(ref callCount)))
            .ToArray();

        var results = await Task.WhenAll(requests);
        
        var miniReservations = results.Where(r => r.ModelName == "gpt-4o-mini").ToArray();
        var ollamaReservations = results.Where(r => r.ModelName == "Ollama").ToArray();

        Assert.Equal(2, miniReservations.Length);
        Assert.Equal(3, ollamaReservations.Length);
        Assert.Equal(5, callCount);

        var budget = await ReadBudgetAsync(userId);
        // Budget should be exhausted by the 2 gpt-4o-mini reservations (2 * 0.00054 = 0.00108)
        Assert.True(budget.ConsumedAmount.Amount + budget.ReservedAmount.Amount <= budget.TotalLimit.Amount);
    }

    private async Task<Guid> SeedBudgetAsync(decimal limit)
    {
        var userId = Guid.NewGuid();
        var repository = factory.Services.GetRequiredService<TestUserBudgetRepository>();
        await repository.AddAsync(new UserBudget(userId, Money.FromDecimal(limit)), CancellationToken.None);
        return userId;
    }

    private async Task<CostGovernorResult> ExecuteBillableRequestAsync(
        Guid userId,
        int estimatedTokens,
        string primaryModelName,
        Action llmCall)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var governor = scope.ServiceProvider.GetRequiredService<ICostGovernor>();
        var result = await governor.EstimateAndReserveAsync(
            userId.ToString(),
            estimatedTokens,
            primaryModelName);
        if (result.Status == CostGovernorStatus.Reserved)
        {
            llmCall();
        }

        return result;
    }

    private async Task CommitAsync(Guid userId, Guid reservationId, decimal actualCost)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var governor = scope.ServiceProvider.GetRequiredService<ICostGovernor>();
        await governor.CommitAsync(userId.ToString(), reservationId, actualCost);
    }

    private async Task<UserBudget> ReadBudgetAsync(Guid userId)
    {
        var repository = factory.Services.GetRequiredService<TestUserBudgetRepository>();
        return (await repository.GetByUserIdAsync(userId, CancellationToken.None))!;
    }
        [Fact]
    public async Task OverBudgetCompletion_CapsCommitAndReleasesReservation()
    {
        var userId = await SeedBudgetAsync(0.01m); 
        
        var result = await ExecuteBillableRequestAsync(userId, 1000, "gpt-4o-mini", () => { });
        Assert.Equal(CostGovernorStatus.Reserved, result.Status);
        
        await using var scope = factory.Services.CreateAsyncScope();
        var governor = scope.ServiceProvider.GetRequiredService<ICostGovernor>();
        
        // Actual usage that results in cost > budget limit (0.01)
        // gpt-4o-mini: 0.00015 prompt, 0.0006 completion. 
        // 10000 prompt (0.0015) + 15000 completion (0.009) = 0.0105 > 0.01
        var actualUsage = new EquipFlow.Domain.Budget.ValueObjects.TokenUsage(10000, 15000); 
        
        var reconciled = await governor.ReconcileAsync(
            userId.ToString(), 
            result.ReservationId!.Value.ToString(), 
            actualUsage, 
            "gpt-4o-mini");

        Assert.True(reconciled);

        var budget = await ReadBudgetAsync(userId);
        Assert.Equal(0m, budget.ReservedAmount.Amount); // Σ(active reservations) == 0
        Assert.Equal(0.01m, budget.ConsumedAmount.Amount); // Capped at limit
        Assert.Equal(0m, budget.AvailableAmount.Amount);
    }
}

public sealed class CostGovernorWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<EquipFlowDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<EquipFlowDbContext>>();
            
            services.AddDbContext<EquipFlowDbContext>(options =>
                options.UseInMemoryDatabase("cost-governor-eval"));

            services.RemoveAll<IUserBudgetRepository>();
            services.AddSingleton<TestUserBudgetRepository>();
            services.AddSingleton<IUserBudgetRepository>(services =>
                services.GetRequiredService<TestUserBudgetRepository>());

            // Bypass EF InMemory transaction limitations
            services.RemoveAll<EquipFlow.Application.Ports.ITransactionManager>();
            services.AddScoped<EquipFlow.Application.Ports.ITransactionManager, NoOpTransactionManager>();
        });
    }
}



public sealed class TestUserBudgetRepository : IUserBudgetRepository
{
    private readonly ConcurrentDictionary<Guid, UserBudget> budgets = new();

    public Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(budgets.GetValueOrDefault(userId));

    public Task<UserBudget?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(budgets.GetValueOrDefault(userId));

    public Task<IEnumerable<UserBudget>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<UserBudget>>(budgets.Values.ToArray());

    public Task AddAsync(UserBudget budget, CancellationToken cancellationToken)
    {
        budgets[budget.UserId] = budget;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserBudget budget, CancellationToken cancellationToken)
    {
        budgets[budget.UserId] = budget;
        return Task.CompletedTask;
    }
    public Task<IEnumerable<UserBudget>> GetBudgetsNeedingResetAsync(DateTimeOffset currentDate, CancellationToken ct) =>
        Task.FromResult<IEnumerable<UserBudget>>(Array.Empty<UserBudget>());
    public Task<IEnumerable<UserBudget>> GetBudgetsWithStaleReservationsAsync(DateTimeOffset cutoffDate, CancellationToken ct)
    {
        var staleBudgets = budgets.Values
            .Where(b => b.Reservations.Any(r => r.CreatedAt < cutoffDate.UtcDateTime))
            .ToList();
        return Task.FromResult<IEnumerable<UserBudget>>(staleBudgets);
    }

}
public sealed class NoOpTransactionManager : EquipFlow.Application.Ports.ITransactionManager
{
    public Task<EquipFlow.Application.Ports.IUnitOfWork> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<EquipFlow.Application.Ports.IUnitOfWork>(new NoOpUnitOfWork());

    private sealed class NoOpUnitOfWork : EquipFlow.Application.Ports.IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}


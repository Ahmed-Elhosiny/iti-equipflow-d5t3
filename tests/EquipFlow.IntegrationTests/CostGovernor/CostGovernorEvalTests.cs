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

        var result = await ExecuteBillableRequestAsync(userId, 1000, 0.01m, () => callCount++);

        Assert.Equal(CostGovernorStatus.Reserved, result.Status);
        Assert.Equal(1, callCount);
        await CommitAsync(userId, result.ReservationId!.Value, 0.01m);

        var budget = await ReadBudgetAsync(userId);
        Assert.Equal(0.01m, budget.ConsumedAmount.Amount);
        Assert.Equal(0m, budget.ReservedAmount.Amount);
    }

    [Fact]
    public async Task InsufficientBudget_ReturnsStructuredRefusalWithoutCallingLlmOrDeductingBudget()
    {
        var userId = await SeedBudgetAsync(0.01m);
        var callCount = 0;

        var result = await ExecuteBillableRequestAsync(userId, 3000, 0.01m, () => callCount++);

        Assert.Equal(CostGovernorStatus.Blocked, result.Status);
        Assert.Equal("budget_exhausted", result.Reason);
        Assert.Equal(0, callCount);

        var budget = await ReadBudgetAsync(userId);
        Assert.Equal(0m, budget.ConsumedAmount.Amount);
        Assert.Equal(0m, budget.ReservedAmount.Amount);
    }

    [Fact]
    public async Task FallbackRouting_UsesCheaperModelWhenPrimaryExceedsBudget()
    {
        var userId = await SeedBudgetAsync(0.02m);
        var callCount = 0;

        var result = await ExecuteBillableRequestAsync(userId, 3000, 0.01m, () => callCount++);

        Assert.Equal(CostGovernorStatus.Reserved, result.Status);
        Assert.Equal("fallback", result.ModelName);
        Assert.Equal(0.018m, result.EstimatedCost);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ConcurrentRequests_ReserveOnlyWithinAvailableBudget()
    {
        var userId = await SeedBudgetAsync(0.072m);
        var callCount = 0;
        var requests = Enumerable.Range(0, 5)
            .Select(_ => ExecuteBillableRequestAsync(userId, 3000, 0.01m, () => Interlocked.Increment(ref callCount)))
            .ToArray();

        var results = await Task.WhenAll(requests);
        var successful = results.Where(result => result.Status == CostGovernorStatus.Reserved).ToArray();
        var blocked = results.Where(result => result.Status == CostGovernorStatus.Blocked).ToArray();

        Assert.Equal(2, successful.Length);
        Assert.Equal(3, blocked.Length);
        Assert.Equal(2, callCount);
        Assert.All(blocked, result => Assert.Equal("budget_exhausted", result.Reason));

        var budget = await ReadBudgetAsync(userId);
        Assert.Equal(0m, budget.AvailableAmount.Amount);
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
        decimal pricePerThousandTokens,
        Action llmCall)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var governor = scope.ServiceProvider.GetRequiredService<ICostGovernor>();
        var result = await governor.EstimateAndReserveAsync(
            userId.ToString(),
            estimatedTokens,
            pricePerThousandTokens);
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
            services.RemoveAll<IUserBudgetRepository>();
            services.AddSingleton<TestUserBudgetRepository>();
            services.AddSingleton<IUserBudgetRepository>(services =>
                services.GetRequiredService<TestUserBudgetRepository>());
        });
    }
}

public sealed class TestUserBudgetRepository : IUserBudgetRepository
{
    private readonly ConcurrentDictionary<Guid, UserBudget> budgets = new();

    public Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
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
}

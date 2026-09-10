using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.Budget.Services;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Ports;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace EquipFlow.Application.Tests;

public sealed class CostGovernorServiceTests
{
    [Fact]
    public async Task EstimateAndReserveAsync_UsesCheaperModelWhenPrimaryDoesNotFit()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeUserBudgetRepository(new UserBudget(userId, Money.FromDecimal(0.02m)));
        var router = new FakeModelRouter(new ModelRoute("cheap", 0.005m));
        var service = CreateService(repository, router);

        var result = await service.EstimateAndReserveAsync(userId.ToString(), 3000, 0.01m);

        Assert.Equal(CostGovernorStatus.Reserved, result.Status);
        Assert.Equal("cheap", result.ModelName);
        Assert.NotNull(result.ReservationId);
        Assert.Equal(0.018m, result.EstimatedCost);
    }

    [Fact]
    public async Task EstimateAndReserveAsync_ReturnsCacheHitAfterModelsDoNotFit()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeUserBudgetRepository(new UserBudget(userId, Money.FromDecimal(0.01m)));
        var cache = new FakeCachePort(new SemanticCacheMatch("request", "cached answer"));
        var service = CreateService(repository, new FakeModelRouter(null), cache);

        var result = await service.EstimateAndReserveAsync(userId.ToString(), 3000, 0.01m, semanticQuery: "request");

        Assert.Equal(CostGovernorStatus.Cached, result.Status);
        Assert.Equal("cached answer", result.CachedResponse);
        Assert.Equal("request", cache.LastQuery);
    }

    [Fact]
    public async Task EstimateAndReserveAsync_ReturnsStructuredRefusalWhenCascadeFails()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeUserBudgetRepository(new UserBudget(userId, Money.FromDecimal(0.01m)));
        var service = CreateService(repository, new FakeModelRouter(null));

        var result = await service.EstimateAndReserveAsync(userId.ToString(), 3000, 0.01m);

        Assert.Equal(CostGovernorStatus.Blocked, result.Status);
        Assert.Equal("budget_exhausted", result.Reason);
        Assert.Equal(0.018m, result.EstimatedCost);
        Assert.Equal(0.01m, result.RemainingBudget);
    }

    private static CostGovernorService CreateService(
        FakeUserBudgetRepository repository,
        IModelRouter router,
        ICachePort? cache = null) =>
        new(
            repository,
            NullLogger<CostGovernorService>.Instance,
            router,
            cache);

    private sealed class FakeModelRouter(ModelRoute? route) : IModelRouter
    {
        public Task<ModelRoute?> GetCheaperModelAsync(
            decimal currentPricePerThousandTokens,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(route);
    }

    private sealed class FakeCachePort(SemanticCacheMatch? match) : ICachePort
    {
        public string? LastQuery { get; private set; }

        public Task<SemanticCacheMatch?> FindSemanticMatchAsync(
            string? semanticQuery,
            CancellationToken cancellationToken = default)
        {
            LastQuery = semanticQuery;
            return Task.FromResult(match);
        }
    }

    private sealed class FakeUserBudgetRepository(UserBudget budget) : IUserBudgetRepository
    {
        public Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserBudget?>(userId == budget.UserId ? budget : null);

        public Task<IEnumerable<UserBudget>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<UserBudget>>(new[] { budget });

        public Task AddAsync(UserBudget newBudget, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateAsync(UserBudget updatedBudget, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

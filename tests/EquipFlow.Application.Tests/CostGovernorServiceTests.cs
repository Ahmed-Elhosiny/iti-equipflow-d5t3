using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.Budget.Services;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Ports;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using BudgetTokenUsage = EquipFlow.Domain.Budget.ValueObjects.TokenUsage;

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

    [Fact]
    public async Task ReconcileAsync_WhenActualCostLessThanEstimated_RefundsDifference()
    {
        var userId = Guid.NewGuid();
        var budget = new UserBudget(userId, Money.FromDecimal(1m));
        var repository = new FakeUserBudgetRepository(budget);
        var service = CreateService(repository, new FakeModelRouter(null));

        var reservation = await service.EstimateAndReserveAsync(userId.ToString(), 1000, 0.01m);

        var reconciled = await service.ReconcileAsync(
            reservation.ReservationId!.Value.ToString(),
            BudgetTokenUsage.FromActual(100, 100),
            "gpt-4o-mini");

        Assert.True(reconciled);
        Assert.Equal(0.000075m, budget.ConsumedAmount.Amount);
        Assert.Equal(0.999925m, budget.AvailableAmount.Amount);
        Assert.Empty(budget.Reservations);
    }

    [Fact]
    public async Task ReconcileAsync_WhenActualCostMoreThanEstimated_ChargesExtra()
    {
        var userId = Guid.NewGuid();
        var budget = new UserBudget(userId, Money.FromDecimal(1m));
        var repository = new FakeUserBudgetRepository(budget);
        var service = CreateService(repository, new FakeModelRouter(null));

        var reservation = await service.EstimateAndReserveAsync(userId.ToString(), 1000, 0.0001m);

        var reconciled = await service.ReconcileAsync(
            reservation.ReservationId!.Value.ToString(),
            BudgetTokenUsage.FromActual(1000, 1000),
            "gpt-4o-mini");

        Assert.True(reconciled);
        Assert.Equal(0.00075m, budget.ConsumedAmount.Amount);
        Assert.Equal(0.99925m, budget.AvailableAmount.Amount);
        Assert.Empty(budget.Reservations);
    }
        [Fact]
    public async Task EstimateAndReserveAsync_ReturnsCacheHitAndDoesNotReserveBudget_WhenCacheMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialBudget = Money.FromDecimal(10m);
        var budget = new UserBudget(userId, initialBudget);
        var repository = new FakeUserBudgetRepository(budget);
        var cache = new FakeCachePort(new SemanticCacheMatch("pump overheating", "Check bearings and coolant."));
        var service = CreateService(repository, new FakeModelRouter(null), cache);

        // Act
        var result = await service.EstimateAndReserveAsync(
            userId.ToString(), 
            3000, 
            0.01m, 
            semanticQuery: "pump overheating");

        // Assert
        Assert.Equal(CostGovernorStatus.Cached, result.Status);
        Assert.Equal("Check bearings and coolant.", result.CachedResponse);
        Assert.Equal(0m, result.EstimatedCost); // Zero cost
        Assert.Equal(10m, result.RemainingBudget); // Budget untouched
        Assert.Empty(budget.Reservations); // No reservation created
    }

    [Fact]
    public async Task EstimateAndReserveAsync_ReservesBudget_WhenCacheMisses()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialBudget = Money.FromDecimal(10m);
        var budget = new UserBudget(userId, initialBudget);
        var repository = new FakeUserBudgetRepository(budget);
        var cache = new FakeCachePort(null); // Cache miss
        var service = CreateService(repository, new FakeModelRouter(null), cache);

        // Act
        var result = await service.EstimateAndReserveAsync(
            userId.ToString(), 
            3000, 
            0.01m, 
            semanticQuery: "unique query");

        // Assert
        Assert.Equal(CostGovernorStatus.Reserved, result.Status);
        Assert.NotNull(result.ReservationId);
        Assert.NotEmpty(budget.Reservations); // Reservation created
        Assert.True(budget.AvailableAmount.Amount < 10m); // Budget reduced
    }
    [Fact]
    public async Task EstimateAndReserveAsync_ReturnsBlocked_WhenBudgetStoreFails()
    {
        var userId = Guid.NewGuid();
        var repository = new ThrowingUserBudgetRepository();
        var service = CreateService(repository, new FakeModelRouter(null));

        var result = await service.EstimateAndReserveAsync(userId.ToString(), 3000, 0.01m);

        Assert.Equal(CostGovernorStatus.Blocked, result.Status);
        Assert.Equal("budget_store_unavailable", result.Reason);
    }

    [Fact]
    public async Task ReconcileAsync_ReturnsFalse_WhenBudgetStoreFails()
    {
        var userId = Guid.NewGuid();
        var repository = new ThrowingUserBudgetRepository();
        var service = CreateService(repository, new FakeModelRouter(null));

        var reconciled = await service.ReconcileAsync(
            Guid.NewGuid().ToString(),
            BudgetTokenUsage.FromActual(100, 100),
            "gpt-4o-mini");

        Assert.False(reconciled);
    }

           private static CostGovernorService CreateService(
        IUserBudgetRepository repository, // <-- Changed from FakeUserBudgetRepository
        IModelRouter router,
        ICachePort? cache = null) =>
        new(
            repository,
            new FakeRunSpendRepository(),
            new FakeTransactionManager(),
            NullLogger<CostGovernorService>.Instance,
            router,
            cache);

    private sealed class FakeTransactionManager : ITransactionManager
    {
        public Task<IUnitOfWork> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWork>(new FakeUnitOfWork());

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

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
        public string? LastAddedQuery { get; private set; }
        public string? LastAddedResponse { get; private set; }

        public Task AddAsync(string query, string response, CancellationToken cancellationToken = default)
        {
            LastAddedQuery = query;
            LastAddedResponse = response;
            return Task.CompletedTask;
        }

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

        public Task<UserBudget?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserBudget?>(userId == budget.UserId ? budget : null);

        public Task<IEnumerable<UserBudget>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<UserBudget>>(new[] { budget });

        public Task AddAsync(UserBudget newBudget, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateAsync(UserBudget updatedBudget, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IEnumerable<UserBudget>> GetBudgetsNeedingResetAsync(DateTimeOffset currentDate, CancellationToken ct) =>
        Task.FromResult<IEnumerable<UserBudget>>(Array.Empty<UserBudget>());
    }
        private sealed class FakeRunSpendRepository : IRunSpendRepository
    {
        public Task AddAsync(RunSpend spend, CancellationToken ct) => Task.CompletedTask;
        
        public Task<IEnumerable<RunSpend>> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<RunSpend>>(Array.Empty<RunSpend>());
    }
       private sealed class ThrowingUserBudgetRepository : IUserBudgetRepository
    {
        public Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) => throw new InvalidOperationException("DB down");
        public Task<UserBudget?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken) => throw new InvalidOperationException("DB down");
        public Task<IEnumerable<UserBudget>> GetAllAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("DB down");
        public Task AddAsync(UserBudget budget, CancellationToken cancellationToken) => throw new InvalidOperationException("DB down");
        public Task UpdateAsync(UserBudget budget, CancellationToken cancellationToken) => throw new InvalidOperationException("DB down");
        public Task<IEnumerable<UserBudget>> GetBudgetsNeedingResetAsync(DateTimeOffset currentDate, CancellationToken ct) => throw new InvalidOperationException("DB down");
    }
    
}


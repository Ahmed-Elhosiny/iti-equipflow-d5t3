using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Options;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.Extensions.Logging;
using AgentTokenUsage = EquipFlow.Application.Agentic.Abstractions.TokenUsage;

namespace EquipFlow.Application.Budget.Services;

public sealed class CostGovernorService(
    IUserBudgetRepository userBudgetRepository,
    ITransactionManager transactionManager,
    ILogger<CostGovernorService> logger,
    IModelRouter? modelRouter = null,
    ICachePort? cachePort = null) : ICostGovernor
{
    private const decimal SafetyMargin = 1.2m;
    private const decimal MockFallbackRateMultiplier = 0.5m;
    private static readonly Money DefaultBudgetLimit = Money.FromDecimal(10m);
    private readonly OpenAIOptions _openAIOptions = new("gpt-4o-mini", string.Empty, null);

    public async Task<CostGovernorResult> EstimateAndReserveAsync(
        string userId,
        int estimatedTokens,
        decimal pricePerThousandTokens,
        CancellationToken cancellationToken = default,
        string? semanticQuery = null)
    {
        var parsedUserId = ParseUserId(userId);

        // 1. CHECK SEMANTIC CACHE FIRST (Zero Cost Fallback) - Read-only, no lock needed
        var cacheMatch = cachePort is null
            ? null
            : await cachePort.FindSemanticMatchAsync(semanticQuery, cancellationToken);
            
        if (cacheMatch is not null)
        {
            var budgetForCache = await GetOrCreateBudgetAsync(parsedUserId, cancellationToken);
            return CostGovernorResult.Cached(cacheMatch.Response, 0m, budgetForCache.AvailableAmount.Amount);
        }

        // 2. BEGIN TRANSACTION FOR PESSIMISTIC LOCKING
        await using var uow = await transactionManager.BeginTransactionAsync(cancellationToken);
        var budget = await GetOrCreateBudgetForUpdateAsync(parsedUserId, cancellationToken);
        
        var primaryCost = EstimateCost(estimatedTokens, pricePerThousandTokens);

        // 3. TRY PRIMARY MODEL
        var primaryReservation = await TryReserveAsync(budget, primaryCost, "primary", cancellationToken);
        if (primaryReservation is not null)
        {
            await uow.CommitAsync(cancellationToken);
            return primaryReservation;
        }

        // 4. TRY FALLBACK MODEL
        var fallbackModel = modelRouter is null
            ? null
            : await modelRouter.GetCheaperModelAsync(pricePerThousandTokens, cancellationToken);
        fallbackModel ??= new ModelRoute("fallback", pricePerThousandTokens * MockFallbackRateMultiplier);
            
        if (fallbackModel is not null && fallbackModel.PricePerThousandTokens < pricePerThousandTokens)
        {
            var fallbackCost = EstimateCost(estimatedTokens, fallbackModel.PricePerThousandTokens);
            var fallbackReservation = await TryReserveAsync(budget, fallbackCost, fallbackModel.ModelName, cancellationToken);
            if (fallbackReservation is not null)
            {
                await uow.CommitAsync(cancellationToken);
                return fallbackReservation;
            }
            primaryCost = fallbackCost;
        }

        // 5. BLOCKED (Transaction rolls back automatically on dispose)
        return CostGovernorResult.Blocked(primaryCost.Amount, budget.AvailableAmount.Amount);
    }

    private async Task<CostGovernorResult?> TryReserveAsync(
        UserBudget budget,
        Money estimatedCost,
        string modelName,
        CancellationToken cancellationToken)
    {
        var reservationId = Guid.NewGuid();
        if (!budget.TryReserve(reservationId, estimatedCost)) return null;

        await userBudgetRepository.UpdateAsync(budget, cancellationToken);
        logger.LogInformation("Reserved {AmountUsd} USD with model {ModelName} using reservation {ReservationId}.",
            estimatedCost.Amount, modelName, reservationId);

        return CostGovernorResult.Reserved(reservationId, modelName, estimatedCost.Amount, budget.AvailableAmount.Amount);
    }

    private static Money EstimateCost(int estimatedTokens, decimal pricePerThousandTokens) =>
        Money.FromDecimal(estimatedTokens / 1000m * pricePerThousandTokens * SafetyMargin);

    public async Task<bool> ReconcileAsync(
        string reservationId,
        EquipFlow.Domain.Budget.ValueObjects.TokenUsage actualUsage,
        string modelUsed,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(reservationId, out var parsedReservationId)) return false;

        // Find budget without lock first to identify the UserId
        var budgets = await userBudgetRepository.GetAllAsync(cancellationToken);
        var budget = budgets.FirstOrDefault(item => item.Reservations.Any(r => r.Id == parsedReservationId));
            
        if (budget is null)
        {
            logger.LogWarning("Unable to reconcile missing reservation {ReservationId}.", reservationId);
            return false;
        }

        await using var uow = await transactionManager.BeginTransactionAsync(cancellationToken);
        var lockedBudget = await userBudgetRepository.GetByUserIdForUpdateAsync(budget.UserId, cancellationToken);
        
        if (lockedBudget is null) return false;

        var reservation = lockedBudget.Reservations.FirstOrDefault(item => item.Id == parsedReservationId);
        if (reservation is null) return false;

        var pricing = _openAIOptions.ModelPricing.TryGetValue(modelUsed, out var modelPricing)
            ? modelPricing
            : (_openAIOptions.PromptTokenPricePer1K, _openAIOptions.CompletionTokenPricePer1K);
            
        var actualCost = Money.FromDecimal(
            actualUsage.PromptTokens / 1000m * pricing.Item1 + actualUsage.CompletionTokens / 1000m * pricing.Item2);

        if (actualCost > reservation.EstimatedCost && lockedBudget.AvailableAmount < actualCost - reservation.EstimatedCost)
        {
            logger.LogWarning("Unable to reconcile reservation {ReservationId}: insufficient budget for extra cost.", reservationId);
            return false;
        }

        lockedBudget.Commit(parsedReservationId, actualCost);
        await userBudgetRepository.UpdateAsync(lockedBudget, cancellationToken);
        await uow.CommitAsync(cancellationToken);
        
        logger.LogInformation("Reconciled reservation {ReservationId} for model {ModelUsed}.", reservationId, modelUsed);
        return true;
    }

    public async Task CommitAsync(string userId, Guid reservationId, decimal actualUsageCost, CancellationToken cancellationToken = default)
    {
        await using var uow = await transactionManager.BeginTransactionAsync(cancellationToken);
        var budget = await GetRequiredBudgetForUpdateAsync(ParseUserId(userId), cancellationToken);
        budget.Commit(reservationId, Money.FromDecimal(actualUsageCost));
        await userBudgetRepository.UpdateAsync(budget, cancellationToken);
        await uow.CommitAsync(cancellationToken);
    }

    public async Task ReleaseAsync(string userId, Guid reservationId, CancellationToken cancellationToken = default)
    {
        await using var uow = await transactionManager.BeginTransactionAsync(cancellationToken);
        var budget = await GetRequiredBudgetForUpdateAsync(ParseUserId(userId), cancellationToken);
        budget.Release(reservationId);
        await userBudgetRepository.UpdateAsync(budget, cancellationToken);
        await uow.CommitAsync(cancellationToken);
    }

    public async Task<BudgetCheckResult> CheckBudgetAsync(string userId, EstimatedCost estimatedCost, CancellationToken cancellationToken = default)
    {
        var budget = await GetOrCreateBudgetAsync(ParseUserId(userId), cancellationToken);
        var requestedAmount = Money.FromDecimal(estimatedCost.AmountUsd);
        return budget.AvailableAmount >= requestedAmount
            ? new BudgetCheckResult(true)
            : new BudgetCheckResult(false, "Insufficient budget for the estimated cost.");
    }

    public Task RecordUsageAsync(string userId, AgentTokenUsage usage, string runId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Usage cannot be recorded without the reservation id returned by EstimateAndReserveAsync.");

    private async Task<UserBudget> GetOrCreateBudgetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var budget = await userBudgetRepository.GetByUserIdAsync(userId, cancellationToken);
        if (budget is not null) return budget;

        var newBudget = new UserBudget(userId, DefaultBudgetLimit);
        await userBudgetRepository.AddAsync(newBudget, cancellationToken);
        await userBudgetRepository.UpdateAsync(newBudget, cancellationToken);
        return newBudget;
    }

    private async Task<UserBudget> GetOrCreateBudgetForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var budget = await userBudgetRepository.GetByUserIdForUpdateAsync(userId, cancellationToken);
        if (budget is not null) return budget;

        var newBudget = new UserBudget(userId, DefaultBudgetLimit);
        await userBudgetRepository.AddAsync(newBudget, cancellationToken);
        await userBudgetRepository.UpdateAsync(newBudget, cancellationToken);
        return newBudget;
    }

    private async Task<UserBudget> GetRequiredBudgetForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var budget = await userBudgetRepository.GetByUserIdForUpdateAsync(userId, cancellationToken);
        return budget ?? throw new InvalidOperationException($"No budget exists for user '{userId}'.");
    }

    private static Guid ParseUserId(string userId) =>
        Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : throw new ArgumentException("User id must be a valid GUID.", nameof(userId));
}
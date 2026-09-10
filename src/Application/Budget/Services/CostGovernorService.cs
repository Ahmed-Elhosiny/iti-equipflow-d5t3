using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.Ports;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.Extensions.Logging;
using AgentTokenUsage = EquipFlow.Application.Agentic.Abstractions.TokenUsage;

namespace EquipFlow.Application.Budget.Services;

public sealed class CostGovernorService(
    IUserBudgetRepository userBudgetRepository,
    ILogger<CostGovernorService> logger,
    IModelRouter? modelRouter = null,
    ICachePort? cachePort = null) : ICostGovernor
{
    private const decimal SafetyMargin = 1.2m;
    private const decimal MockFallbackRateMultiplier = 0.5m;
    private static readonly Money DefaultBudgetLimit = Money.FromDecimal(10m);

    public async Task<CostGovernorResult> EstimateAndReserveAsync(
        string userId,
        int estimatedTokens,
        decimal pricePerThousandTokens,
        CancellationToken cancellationToken = default,
        string? semanticQuery = null)
    {
        var parsedUserId = ParseUserId(userId);
        var budget = await GetOrCreateBudgetAsync(parsedUserId, cancellationToken);
        var primaryCost = EstimateCost(estimatedTokens, pricePerThousandTokens);

        var primaryReservation = await TryReserveAsync(
            budget,
            primaryCost,
            "primary",
            cancellationToken);
        if (primaryReservation is not null)
        {
            return primaryReservation;
        }

        var fallbackModel = modelRouter is null
            ? null
            : await modelRouter.GetCheaperModelAsync(pricePerThousandTokens, cancellationToken);
        fallbackModel ??= new ModelRoute(
            "fallback",
            pricePerThousandTokens * MockFallbackRateMultiplier);
        if (fallbackModel is not null && fallbackModel.PricePerThousandTokens < pricePerThousandTokens)
        {
            var fallbackCost = EstimateCost(estimatedTokens, fallbackModel.PricePerThousandTokens);
            var fallbackReservation = await TryReserveAsync(
                budget,
                fallbackCost,
                fallbackModel.ModelName,
                cancellationToken);
            if (fallbackReservation is not null)
            {
                return fallbackReservation;
            }

            primaryCost = fallbackCost;
        }

        var cacheMatch = cachePort is null
            ? null
            : await cachePort.FindSemanticMatchAsync(semanticQuery, cancellationToken);
        if (cacheMatch is not null)
        {
            return CostGovernorResult.Cached(
                cacheMatch.Response,
                primaryCost.Amount,
                budget.AvailableAmount.Amount);
        }

        return CostGovernorResult.Blocked(primaryCost.Amount, budget.AvailableAmount.Amount);
    }

    private async Task<CostGovernorResult?> TryReserveAsync(
        UserBudget budget,
        Money estimatedCost,
        string modelName,
        CancellationToken cancellationToken)
    {
        var reservationId = Guid.NewGuid();
        if (!budget.TryReserve(reservationId, estimatedCost))
        {
            return null;
        }

        await userBudgetRepository.UpdateAsync(budget, cancellationToken);
        logger.LogInformation(
            "Reserved {AmountUsd} USD with model {ModelName} using reservation {ReservationId}.",
            estimatedCost.Amount,
            modelName,
            reservationId);

        return CostGovernorResult.Reserved(
            reservationId,
            modelName,
            estimatedCost.Amount,
            budget.AvailableAmount.Amount);
    }

    private static Money EstimateCost(int estimatedTokens, decimal pricePerThousandTokens) =>
        Money.FromDecimal(estimatedTokens / 1000m * pricePerThousandTokens * SafetyMargin);

    public async Task CommitAsync(
        string userId,
        Guid reservationId,
        decimal actualUsageCost,
        CancellationToken cancellationToken = default)
    {
        var budget = await GetRequiredBudgetAsync(ParseUserId(userId), cancellationToken);
        budget.Commit(reservationId, Money.FromDecimal(actualUsageCost));
        await userBudgetRepository.UpdateAsync(budget, cancellationToken);
    }

    public async Task ReleaseAsync(
        string userId,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var budget = await GetRequiredBudgetAsync(ParseUserId(userId), cancellationToken);
        budget.Release(reservationId);
        await userBudgetRepository.UpdateAsync(budget, cancellationToken);
    }

    public async Task<BudgetCheckResult> CheckBudgetAsync(
        string userId,
        EstimatedCost estimatedCost,
        CancellationToken cancellationToken = default)
    {
        var budget = await GetOrCreateBudgetAsync(ParseUserId(userId), cancellationToken);
        var requestedAmount = Money.FromDecimal(estimatedCost.AmountUsd);

        return budget.AvailableAmount >= requestedAmount
            ? new BudgetCheckResult(true)
            : new BudgetCheckResult(false, "Insufficient budget for the estimated cost.");
    }

    public Task RecordUsageAsync(
        string userId,
        AgentTokenUsage usage,
        string runId,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Usage cannot be recorded without the reservation id returned by EstimateAndReserveAsync.");

    private async Task<UserBudget> GetOrCreateBudgetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var budget = await userBudgetRepository.GetByUserIdAsync(userId, cancellationToken);
        if (budget is not null)
        {
            return budget;
        }

        var newBudget = new UserBudget(userId, DefaultBudgetLimit);
        await userBudgetRepository.AddAsync(newBudget, cancellationToken);
        return newBudget;
    }

    private async Task<UserBudget> GetRequiredBudgetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var budget = await userBudgetRepository.GetByUserIdAsync(userId, cancellationToken);
        return budget ?? throw new InvalidOperationException($"No budget exists for user '{userId}'.");
    }

    private static Guid ParseUserId(string userId) =>
        Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : throw new ArgumentException("User id must be a valid GUID.", nameof(userId));
}
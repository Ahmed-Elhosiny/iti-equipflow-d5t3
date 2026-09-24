using System.Threading;
using System.Threading.Tasks;
using EquipFlow.Domain.Budget.ValueObjects;

namespace EquipFlow.Application.Agentic.Abstractions;

public interface ICostGovernor
{
    Task<CostGovernorResult> EstimateAndReserveAsync(
        string userId,
        int estimatedTokens,
        decimal pricePerThousandTokens,
        CancellationToken cancellationToken = default,
        string? semanticQuery = null);

    Task<bool> CommitAsync( // <-- CHANGED TO Task<bool>
        string userId,
        Guid reservationId,
        decimal actualUsageCost,
        string? runId = null,
        CancellationToken cancellationToken = default);

    Task<bool> ReconcileAsync(
        string reservationId,
        EquipFlow.Domain.Budget.ValueObjects.TokenUsage actualUsage,
        string modelUsed,
        string? runId = null,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        string userId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<BudgetCheckResult> CheckBudgetAsync(
        string userId,
        EstimatedCost estimatedCost,
        CancellationToken cancellationToken = default);

    Task RecordUsageAsync(
        string userId,
        TokenUsage usage,
        string runId,
        CancellationToken cancellationToken = default);
}

public sealed record BudgetCheckResult(
    bool IsAllowed,
    string? Reason = null);

public sealed record EstimatedCost(
    decimal AmountUsd,
    int InputTokens,
    int OutputTokens);

public enum CostGovernorStatus
{
    Reserved,
    Cached,
    Blocked
}

public sealed record CostGovernorResult(
    CostGovernorStatus Status,
    Guid? ReservationId,
    string? ModelName,
    string Reason,
    decimal EstimatedCost,
    decimal RemainingBudget,
    string? CachedResponse = null)
{
    public static CostGovernorResult Reserved(
        Guid reservationId,
        string modelName,
        decimal estimatedCost,
        decimal remainingBudget) =>
        new(CostGovernorStatus.Reserved, reservationId, modelName, string.Empty, estimatedCost, remainingBudget);

    public static CostGovernorResult Cached(
        string response,
        decimal estimatedCost,
        decimal remainingBudget) =>
        new(CostGovernorStatus.Cached, null, null, "semantic_cache_hit", estimatedCost, remainingBudget, response);

    // <-- ADDED OPTIONAL REASON PARAMETER
    public static CostGovernorResult Blocked(
        decimal estimatedCost,
        decimal remainingBudget,
        string reason = "budget_exhausted") =>
        new(CostGovernorStatus.Blocked, null, null, reason, estimatedCost, remainingBudget);
}
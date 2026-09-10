using System.Threading;
using System.Threading.Tasks;

namespace EquipFlow.Application.Agentic.Abstractions;

public interface ICostGovernor
{
    Task<CostGovernorResult> EstimateAndReserveAsync(
        string userId,
        int estimatedTokens,
        decimal pricePerThousandTokens,
        CancellationToken cancellationToken = default,
        string? semanticQuery = null);

    Task CommitAsync(
        string userId,
        Guid reservationId,
        decimal actualUsageCost,
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

    public static CostGovernorResult Blocked(
        decimal estimatedCost,
        decimal remainingBudget) =>
        new(CostGovernorStatus.Blocked, null, null, "budget_exhausted", estimatedCost, remainingBudget);
}
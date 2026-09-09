using System.Threading;
using System.Threading.Tasks;

namespace EquipFlow.Application.Agentic.Abstractions;

public interface ICostGovernor
{
    Task<Guid> EstimateAndReserveAsync(
        string userId,
        int estimatedTokens,
        decimal pricePerThousandTokens,
        CancellationToken cancellationToken = default);

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
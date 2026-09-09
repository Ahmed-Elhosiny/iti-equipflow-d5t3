using System.Threading;
using System.Threading.Tasks;

namespace EquipFlow.Application.Agentic.Abstractions;

public interface ICostGovernor
{
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
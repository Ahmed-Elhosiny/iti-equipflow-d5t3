namespace EquipFlow.Application.CostGovernor.Queries.Dtos;

public sealed record BudgetSummaryDto(
    Guid UserId,
    decimal TotalBudget,
    decimal Consumed,
    decimal Reserved,
    decimal Available,
    IReadOnlyList<RunHistoryDto> RecentRuns);
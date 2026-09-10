namespace EquipFlow.Application.CostGovernor.Queries.Dtos;

public sealed record RunHistoryDto(
    Guid RunId,
    DateTime Timestamp,
    string AgentName,
    decimal EstimatedCost,
    decimal ActualCost,
    string Status);
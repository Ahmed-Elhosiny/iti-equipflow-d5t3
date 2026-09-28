using EquipFlow.Application.CostGovernor.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.CostGovernor.Queries;

/// <summary>
/// Query to retrieve the run spend history for a specific user.
/// </summary>
public sealed record GetMySpendQuery(Guid UserId) : IRequest<IEnumerable<RunHistoryDto>>;
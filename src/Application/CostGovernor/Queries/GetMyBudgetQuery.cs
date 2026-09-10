using EquipFlow.Application.CostGovernor.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.CostGovernor.Queries;

public sealed record GetMyBudgetQuery(Guid UserId) : IRequest<BudgetSummaryDto>;
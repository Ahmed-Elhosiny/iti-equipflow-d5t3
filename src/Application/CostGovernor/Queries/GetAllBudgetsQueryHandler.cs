using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.CostGovernor.Queries.Dtos;
using EquipFlow.Domain.Budget;
using MediatR;

namespace EquipFlow.Application.CostGovernor.Queries;

public sealed class GetAllBudgetsQueryHandler(IUserBudgetRepository userBudgetRepository)
    : IRequestHandler<GetAllBudgetsQuery, IEnumerable<BudgetSummaryDto>>
{
    public async Task<IEnumerable<BudgetSummaryDto>> Handle(
        GetAllBudgetsQuery request,
        CancellationToken cancellationToken)
    {
        var budgets = await userBudgetRepository.GetAllAsync(cancellationToken);
        return budgets.Select(MapBudget).ToList();
    }

    private static BudgetSummaryDto MapBudget(UserBudget budget) =>
        new(
            budget.UserId,
            budget.TotalLimit.Amount,
            budget.ConsumedAmount.Amount,
            budget.ReservedAmount.Amount,
            budget.AvailableAmount.Amount,
            budget.Reservations
                .OrderByDescending(reservation => reservation.CreatedAt)
                .Select(reservation => new RunHistoryDto(
                    reservation.Id,
                    reservation.CreatedAt,
                    string.Empty,
                    reservation.EstimatedCost.Amount,
                    0m,
                    "Reserved"))
                .ToList());
}
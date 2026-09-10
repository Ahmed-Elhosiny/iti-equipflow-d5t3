using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.CostGovernor.Queries.Dtos;
using EquipFlow.Domain.Budget;
using MediatR;

namespace EquipFlow.Application.CostGovernor.Queries;

public sealed class GetMyBudgetQueryHandler(IUserBudgetRepository userBudgetRepository)
    : IRequestHandler<GetMyBudgetQuery, BudgetSummaryDto>
{
    public async Task<BudgetSummaryDto> Handle(
        GetMyBudgetQuery request,
        CancellationToken cancellationToken)
    {
        var budget = await userBudgetRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"No budget exists for user '{request.UserId}'.");

        return MapBudget(budget);
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
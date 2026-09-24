using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.CostGovernor.Queries.Dtos;
using EquipFlow.Domain.Budget;
using MediatR;

namespace EquipFlow.Application.CostGovernor.Queries;

public sealed class GetMyBudgetQueryHandler(
    IUserBudgetRepository userBudgetRepository,
    IRunSpendRepository runSpendRepository)
    : IRequestHandler<GetMyBudgetQuery, BudgetSummaryDto>
{
    public async Task<BudgetSummaryDto> Handle(
        GetMyBudgetQuery request,
        CancellationToken cancellationToken)
    {
        var budget = await userBudgetRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"No budget exists for user '{request.UserId}'.");

        var runSpends = await runSpendRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return MapBudget(budget, runSpends);
    }

    private static BudgetSummaryDto MapBudget(UserBudget budget, IEnumerable<RunSpend> runSpends) =>
        new(
            budget.UserId,
            budget.TotalLimit.Amount,
            budget.ConsumedAmount.Amount,
            budget.ReservedAmount.Amount,
            budget.AvailableAmount.Amount,
            runSpends
                .Select(spend => new RunHistoryDto(
                    spend.RunId,
                    spend.CreatedAtUtc.UtcDateTime,
                    spend.ModelUsed,
                    spend.ReservedAmount.Amount,
                    spend.ActualAmount.Amount,
                    "Completed"))
                .ToList());
}
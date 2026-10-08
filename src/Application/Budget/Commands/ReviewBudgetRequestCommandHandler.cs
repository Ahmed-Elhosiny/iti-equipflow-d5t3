using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.Ports;
using EquipFlow.Domain.Budget.ValueObjects;
using EquipFlow.Domain.Enums;
using MediatR;

namespace EquipFlow.Application.Budget.Commands;

public sealed class ReviewBudgetRequestCommandHandler(
    IBudgetIncreaseRequestRepository requestRepository,
    IUserBudgetRepository budgetRepository) : IRequestHandler<ReviewBudgetRequestCommand, bool>
{
    public async Task<bool> Handle(ReviewBudgetRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null || request.Status != BudgetRequestStatus.Pending) return false;

        if (command.IsApproved)
        {
            request.Approve(command.ReviewerId);
            var budget = await budgetRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (budget is not null)
            {
                budget.IncreaseLimit(Money.FromDecimal(request.RequestedAmount));
                await budgetRepository.UpdateAsync(budget, cancellationToken);
            }
        }
        else
        {
            request.Reject(command.ReviewerId);
        }

        await requestRepository.UpdateAsync(request, cancellationToken);
        return true;
    }
}

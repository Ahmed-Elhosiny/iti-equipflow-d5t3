using EquipFlow.Application.Ports;
using EquipFlow.Domain.Entities;
using MediatR;

namespace EquipFlow.Application.Budget.Commands;

public sealed class RequestBudgetIncreaseCommandHandler(IBudgetIncreaseRequestRepository repository) 
    : IRequestHandler<RequestBudgetIncreaseCommand, Guid>
{
    public async Task<Guid> Handle(RequestBudgetIncreaseCommand request, CancellationToken cancellationToken)
    {
        var budgetRequest = new BudgetIncreaseRequest(request.UserId, request.RequestedAmount, request.Reason);
        await repository.AddAsync(budgetRequest, cancellationToken);
        return budgetRequest.Id;
    }
}

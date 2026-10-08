using MediatR;

namespace EquipFlow.Application.Budget.Commands;

public sealed record RequestBudgetIncreaseCommand(Guid UserId, decimal RequestedAmount, string Reason) : IRequest<Guid>;

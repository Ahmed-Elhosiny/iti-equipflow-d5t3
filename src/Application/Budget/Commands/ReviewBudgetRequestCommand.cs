using MediatR;

namespace EquipFlow.Application.Budget.Commands;

public sealed record ReviewBudgetRequestCommand(Guid RequestId, Guid ReviewerId, bool IsApproved) : IRequest<bool>;

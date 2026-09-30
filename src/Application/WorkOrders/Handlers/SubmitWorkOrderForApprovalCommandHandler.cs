using EquipFlow.Application.Common;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Ports;
using MediatR;

namespace EquipFlow.Application.WorkOrders.Handlers;

public class SubmitWorkOrderForApprovalCommandHandler : IRequestHandler<SubmitWorkOrderForApprovalCommand>
{
    private readonly IWorkOrderRepository _repository;

    public SubmitWorkOrderForApprovalCommandHandler(IWorkOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(SubmitWorkOrderForApprovalCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.SubmittedBy))
            throw new ArgumentException("SubmittedBy cannot be empty.", nameof(command.SubmittedBy));

        var workOrder = await _repository.GetByIdNoTrackingAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        workOrder.SubmitForApproval(command.SubmittedBy);

        await _repository.SaveStateTransitionAsync(workOrder, cancellationToken);
    }
}
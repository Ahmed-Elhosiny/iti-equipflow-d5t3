using EquipFlow.Application.Common;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Application.WorkOrders.Handlers;

public class SubmitWorkOrderForApprovalCommandHandler
{
    private readonly IWorkOrderRepository _repository;

    public SubmitWorkOrderForApprovalCommandHandler(IWorkOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(SubmitWorkOrderForApprovalCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.SubmittedBy))
            throw new ArgumentException("SubmittedBy cannot be empty.", nameof(command.SubmittedBy));

        var workOrder = await _repository.GetByIdAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        workOrder.SubmitForApproval();

        await _repository.SaveChangesAsync(cancellationToken);
    }
}

using EquipFlow.Application.Common;
using EquipFlow.Application.WorkOrders.Ports;

namespace EquipFlow.Application.WorkOrders.Handlers;

public class AddSafetyPrerequisiteCommandHandler
{
    private readonly IWorkOrderRepository _repository;

    public AddSafetyPrerequisiteCommandHandler(IWorkOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(AddSafetyPrerequisiteCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Description))
            throw new ArgumentException("Description cannot be empty.", nameof(command.Description));

        var workOrder = await _repository.GetByIdAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        workOrder.AddSafetyPrerequisite(command.Description, command.IsMandatory, command.SortOrder);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}

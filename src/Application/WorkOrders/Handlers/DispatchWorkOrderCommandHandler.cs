using EquipFlow.Application.Common;
using EquipFlow.Application.WorkOrders.Ports;

namespace EquipFlow.Application.WorkOrders.Handlers;

public class DispatchWorkOrderCommandHandler
{
    private readonly IWorkOrderRepository _repository;

    public DispatchWorkOrderCommandHandler(IWorkOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(DispatchWorkOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.DispatcherUserId))
            throw new ArgumentException("DispatcherUserId cannot be empty.", nameof(command.DispatcherUserId));

        var workOrder = await _repository.GetByIdAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        workOrder.MarkDispatched();

        await _repository.SaveChangesAsync(cancellationToken);
    }
}

using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;

namespace EquipFlow.Application.WorkOrders.Handlers;

public class CreateWorkOrderCommandHandler
{
    private readonly IWorkOrderRepository _repository;

    public CreateWorkOrderCommandHandler(IWorkOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> HandleAsync(CreateWorkOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ArgumentException("Title cannot be empty.", nameof(command.Title));
        if (string.IsNullOrWhiteSpace(command.Symptom))
            throw new ArgumentException("Symptom cannot be empty.", nameof(command.Symptom));
        if (string.IsNullOrWhiteSpace(command.EquipmentName))
            throw new ArgumentException("EquipmentName cannot be empty.", nameof(command.EquipmentName));
        if (string.IsNullOrWhiteSpace(command.CreatedBy))
            throw new ArgumentException("CreatedBy cannot be empty.", nameof(command.CreatedBy));

        var workOrder = new WorkOrder(
            command.Title,
            command.Symptom,
            command.EquipmentName,
            command.CreatedBy,
            command.EquipmentAssetNumber,
            command.ManualRevision,
            command.Location);

        await _repository.AddAsync(workOrder, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return workOrder.Id;
    }
}

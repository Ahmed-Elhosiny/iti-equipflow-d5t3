using EquipFlow.Application.Common;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;
using MediatR;

namespace EquipFlow.Application.WorkOrders.Handlers;

public class AddSafetyPrerequisiteCommandHandler : IRequestHandler<AddSafetyPrerequisiteCommand>
{
    private readonly IWorkOrderRepository _repository;

    public AddSafetyPrerequisiteCommandHandler(IWorkOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(AddSafetyPrerequisiteCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Description))
            throw new ArgumentException("Description cannot be empty.", nameof(command.Description));

        // Load without tracking to completely avoid EF Core change tracker concurrency issues
        var workOrder = await _repository.GetByIdNoTrackingAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        // Enforce domain rules explicitly
        if (!workOrder.CanModifySafetyPrerequisites())
            throw new InvalidOperationException($"Cannot add safety prerequisites when work order status is {workOrder.Status}.");

        var prerequisite = new SafetyPrerequisite(command.WorkOrderId, command.Description, command.IsMandatory, command.SortOrder);
        
        // Add directly to the database context, bypassing the parent entity's collection modification
        await _repository.AddSafetyPrerequisiteAsync(prerequisite, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
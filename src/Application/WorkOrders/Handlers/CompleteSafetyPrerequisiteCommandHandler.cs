using EquipFlow.Application.Common;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Ports;
using MediatR;

namespace EquipFlow.Application.WorkOrders.Handlers;

public class CompleteSafetyPrerequisiteCommandHandler : IRequestHandler<CompleteSafetyPrerequisiteCommand>
{
    private readonly IWorkOrderRepository _repository;

    public CompleteSafetyPrerequisiteCommandHandler(IWorkOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(CompleteSafetyPrerequisiteCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.CompletedBy))
            throw new ArgumentException("CompletedBy cannot be empty.", nameof(command.CompletedBy));

        var workOrder = await _repository.GetByIdAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        workOrder.CompleteSafetyPrerequisite(command.PrerequisiteId, command.CompletedBy, command.CompletionNote);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
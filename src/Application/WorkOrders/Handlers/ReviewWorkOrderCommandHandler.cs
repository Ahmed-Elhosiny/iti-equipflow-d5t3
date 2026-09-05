using EquipFlow.Application.Common;
using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Application.WorkOrders.Handlers;

public class ReviewWorkOrderCommandHandler
{
    private readonly IWorkOrderRepository _repository;

    public ReviewWorkOrderCommandHandler(IWorkOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(ReviewWorkOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ReviewerUserId))
            throw new ArgumentException("ReviewerUserId cannot be empty.", nameof(command.ReviewerUserId));

        var workOrder = await _repository.GetByIdAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        switch (command.Decision)
        {
            case WorkOrderReviewDecision.Approve:
                workOrder.Approve(command.ReviewerUserId, command.Comment);
                break;
            case WorkOrderReviewDecision.Reject:
                workOrder.Reject(command.ReviewerUserId, command.Comment);
                break;
            default:
                throw new ArgumentException($"Unknown decision type: {command.Decision}", nameof(command.Decision));
        }

        await _repository.SaveChangesAsync(cancellationToken);
    }
}

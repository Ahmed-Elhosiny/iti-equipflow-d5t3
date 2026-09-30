using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Application.Common;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using MediatR;

namespace EquipFlow.Application.WorkOrders.Queries;

public sealed record GetWorkOrderByIdQuery(Guid WorkOrderId, string RequestingUserId) : IRequest<WorkOrderDto>;

public sealed class GetWorkOrderByIdQueryHandler(IWorkOrderRepository repository)
    : IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDto>
{
    public async Task<WorkOrderDto> Handle(
        GetWorkOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(request.WorkOrderId);

        // Object-level authorization: Fail closed with 404 to prevent resource enumeration
        if (!string.Equals(workOrder.CreatedBy, request.RequestingUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new WorkOrderNotFoundException(request.WorkOrderId);
        }

        return WorkOrderDto.FromDomain(workOrder);
    }
}

public sealed record SafetyPrerequisiteDto(
    Guid Id,
    string Description,
    bool IsMandatory,
    int SortOrder,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? CompletedBy,
    string? CompletionNote);

public sealed record ApprovalActionDto(
    Guid Id,
    ApprovalActionType ActionType,
    string ActorUserId,
    string? Comment,
    DateTimeOffset OccurredAtUtc); // Note: If this fails to compile, change to CreatedAtUtc or Timestamp based on your ApprovalAction.cs domain entity

public sealed record WorkOrderDto(
    Guid Id,
    string Title,
    string Symptom,
    string EquipmentName,
    string? EquipmentAssetNumber,
    string? ManualRevision,
    string? Location,
    WorkOrderStatus Status,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? DecisionBy,
    DateTimeOffset? DecisionAtUtc,
    string? DecisionComment,
    IReadOnlyCollection<SafetyPrerequisiteDto> SafetyPrerequisites,
    IReadOnlyCollection<ApprovalActionDto> ApprovalActions)
{
    public static WorkOrderDto FromDomain(WorkOrder workOrder) => new(
        workOrder.Id,
        workOrder.Title,
        workOrder.Symptom,
        workOrder.EquipmentName,
        workOrder.EquipmentAssetNumber,
        workOrder.ManualRevision,
        workOrder.Location,
        workOrder.Status,
        workOrder.CreatedBy,
        workOrder.CreatedAtUtc,
        workOrder.UpdatedAtUtc,
        workOrder.DecisionBy,
        workOrder.DecisionAtUtc,
        workOrder.DecisionComment,
        workOrder.SafetyPrerequisites.Select(sp => new SafetyPrerequisiteDto(
            sp.Id,
            sp.Description,
            sp.IsMandatory,
            sp.SortOrder,
            sp.CreatedAtUtc,
            sp.CompletedAtUtc,
            sp.CompletedBy,
            sp.CompletionNote)).ToList(),
        workOrder.ApprovalActions.Select(aa => new ApprovalActionDto(
            aa.Id,
            aa.ActionType,
            aa.ActorUserId,
            aa.Comment,
            aa.OccurredAtUtc)).ToList());
}
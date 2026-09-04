using EquipFlow.Domain.Enums;

namespace EquipFlow.Domain.Entities;

public class WorkOrder
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Symptom { get; private set; }
    public string EquipmentName { get; private set; }
    public string? EquipmentAssetNumber { get; private set; }
    public string? ManualRevision { get; private set; }
    public string? Location { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public string? DecisionBy { get; private set; }
    public DateTimeOffset? DecisionAtUtc { get; private set; }
    public string? DecisionComment { get; private set; }

    // Navigation properties
    private readonly ICollection<SafetyPrerequisite> _safetyPrerequisites = new List<SafetyPrerequisite>();
    public IReadOnlyCollection<SafetyPrerequisite> SafetyPrerequisites => (IReadOnlyCollection<SafetyPrerequisite>)_safetyPrerequisites;

    private readonly ICollection<ApprovalAction> _approvalActions = new List<ApprovalAction>();
    public IReadOnlyCollection<ApprovalAction> ApprovalActions => (IReadOnlyCollection<ApprovalAction>)_approvalActions;

    // Computed property
    public bool HasUnmetMandatorySafetyPrerequisites =>
        _safetyPrerequisites.Any(sp => sp.IsMandatory && !sp.CompletedAtUtc.HasValue);

    // EF Core parameterless constructor
    private WorkOrder()
    {
        Id = Guid.Empty;
        Title = string.Empty;
        Symptom = string.Empty;
        EquipmentName = string.Empty;
        CreatedBy = string.Empty;
        Status = WorkOrderStatus.Draft;
        CreatedAtUtc = DateTimeOffset.MinValue;
        UpdatedAtUtc = DateTimeOffset.MinValue;
    }

    public WorkOrder(
        string title,
        string symptom,
        string equipmentName,
        string createdBy,
        string? equipmentAssetNumber = null,
        string? manualRevision = null,
        string? location = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(symptom))
            throw new ArgumentException("Symptom cannot be empty.", nameof(symptom));
        if (string.IsNullOrWhiteSpace(equipmentName))
            throw new ArgumentException("EquipmentName cannot be empty.", nameof(equipmentName));
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy cannot be empty.", nameof(createdBy));

        Id = Guid.NewGuid();
        Title = title;
        Symptom = symptom;
        EquipmentName = equipmentName;
        EquipmentAssetNumber = equipmentAssetNumber;
        ManualRevision = manualRevision;
        Location = location;
        CreatedBy = createdBy;
        Status = WorkOrderStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private void UpdateTimestamp()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private bool CanModifySafetyPrerequisites()
    {
        return Status == WorkOrderStatus.Draft || Status == WorkOrderStatus.Rejected;
    }

    private bool CanCompleteSafetyPrerequisites()
    {
        return Status != WorkOrderStatus.Dispatched && Status != WorkOrderStatus.Cancelled;
    }

    public SafetyPrerequisite AddSafetyPrerequisite(string description, bool isMandatory = true, int sortOrder = 0)
    {
        if (!CanModifySafetyPrerequisites())
            throw new InvalidOperationException($"Cannot add safety prerequisites when work order status is {Status}.");

        var prerequisite = new SafetyPrerequisite(Id, description, isMandatory, sortOrder);
        _safetyPrerequisites.Add(prerequisite);
        UpdateTimestamp();
        return prerequisite;
    }

    public void CompleteSafetyPrerequisite(Guid prerequisiteId, string completedBy, string? completionNote = null)
    {
        if (!CanCompleteSafetyPrerequisites())
            throw new InvalidOperationException($"Cannot complete safety prerequisites when work order status is {Status}.");

        var prerequisite = _safetyPrerequisites.FirstOrDefault(sp => sp.Id == prerequisiteId)
            ?? throw new ArgumentException($"Safety prerequisite with id {prerequisiteId} not found.", nameof(prerequisiteId));

        prerequisite.MarkCompleted(completedBy, completionNote);
        UpdateTimestamp();
    }

    public void SubmitForApproval()
    {
        if (!CanModifySafetyPrerequisites())
            throw new InvalidOperationException($"Cannot submit work order when status is {Status}.");

        if (!_safetyPrerequisites.Any())
            throw new InvalidOperationException("Work order must have at least one safety prerequisite before submission.");

        if (HasUnmetMandatorySafetyPrerequisites)
            throw new InvalidOperationException("All mandatory safety prerequisites must be completed before submission.");

        Status = WorkOrderStatus.PendingApproval;
        _approvalActions.Add(new ApprovalAction(Id, ApprovalActionType.Submitted, CreatedBy));
        UpdateTimestamp();
    }

    public void Approve(string approverUserId, string? comment = null)
    {
        if (Status != WorkOrderStatus.PendingApproval)
            throw new InvalidOperationException($"Only PendingApproval work orders can be approved. Current status: {Status}");

        if (HasUnmetMandatorySafetyPrerequisites)
            throw new InvalidOperationException("All mandatory safety prerequisites must be completed before approval.");

        Status = WorkOrderStatus.Approved;
        DecisionBy = approverUserId;
        DecisionAtUtc = DateTimeOffset.UtcNow;
        DecisionComment = comment;
        _approvalActions.Add(new ApprovalAction(Id, ApprovalActionType.Approved, approverUserId, comment));
        UpdateTimestamp();
    }

    public void Reject(string approverUserId, string? comment = null)
    {
        if (Status != WorkOrderStatus.PendingApproval)
            throw new InvalidOperationException($"Only PendingApproval work orders can be rejected. Current status: {Status}");

        Status = WorkOrderStatus.Rejected;
        DecisionBy = approverUserId;
        DecisionAtUtc = DateTimeOffset.UtcNow;
        DecisionComment = comment;
        _approvalActions.Add(new ApprovalAction(Id, ApprovalActionType.Rejected, approverUserId, comment));
        UpdateTimestamp();
    }

    public void MarkDispatched()
    {
        if (Status != WorkOrderStatus.Approved)
            throw new InvalidOperationException($"Only Approved work orders can be dispatched. Current status: {Status}");

        Status = WorkOrderStatus.Dispatched;
        _approvalActions.Add(new ApprovalAction(Id, ApprovalActionType.Dispatched, DecisionBy ?? CreatedBy));
        UpdateTimestamp();
    }
}

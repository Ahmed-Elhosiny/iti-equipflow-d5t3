namespace EquipFlow.Domain.Entities;

public class SafetyPrerequisite
{
    public Guid Id { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public string Description { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? CompletedBy { get; private set; }
    public string? CompletionNote { get; private set; }

    // EF Core parameterless constructor
    private SafetyPrerequisite()
    {
        Id = Guid.Empty;
        WorkOrderId = Guid.Empty;
        Description = string.Empty;
        IsMandatory = false;
        SortOrder = 0;
        CreatedAtUtc = DateTimeOffset.MinValue;
    }

    public SafetyPrerequisite(Guid workOrderId, string description, bool isMandatory, int sortOrder)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("WorkOrderId cannot be empty.", nameof(workOrderId));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        Id = Guid.NewGuid();
        WorkOrderId = workOrderId;
        Description = description;
        IsMandatory = isMandatory;
        SortOrder = sortOrder;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(string completedBy, string? completionNote = null)
    {
        if (CompletedAtUtc.HasValue)
            throw new InvalidOperationException("This safety prerequisite has already been completed.");

        if (string.IsNullOrWhiteSpace(completedBy))
            throw new ArgumentException("CompletedBy is required.", nameof(completedBy));

        CompletedBy = completedBy;
        CompletionNote = completionNote;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}

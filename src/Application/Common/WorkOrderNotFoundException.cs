namespace EquipFlow.Application.Common;

public class WorkOrderNotFoundException : Exception
{
    public Guid WorkOrderId { get; }

    public WorkOrderNotFoundException(Guid workOrderId)
        : base($"Work order with id '{workOrderId}' was not found.")
    {
        WorkOrderId = workOrderId;
    }
}

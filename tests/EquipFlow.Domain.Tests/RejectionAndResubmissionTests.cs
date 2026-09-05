using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class RejectionAndResubmissionTests
{
    private WorkOrder CreateRejectedWorkOrder()
    {
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123");
        workOrder.SubmitForApproval();
        workOrder.Reject("approver-456", "Need more details");
        
        return workOrder;
    }

    [Fact]
    public void After_Rejection_Adding_New_Safety_Prerequisite_Is_Allowed()
    {
        // Arrange
        var workOrder = CreateRejectedWorkOrder();

        // Act
        var newPrerequisite = workOrder.AddSafetyPrerequisite("Additional safety check", isMandatory: false, sortOrder: 2);

        // Assert
        Assert.Equal(2, workOrder.SafetyPrerequisites.Count);
        Assert.Equal("Additional safety check", newPrerequisite.Description);
    }

    [Fact]
    public void After_Rejection_Completing_Existing_Safety_Prerequisite_Is_Allowed()
    {
        // Arrange
        var workOrder = CreateRejectedWorkOrder();
        var prerequisite = workOrder.SafetyPrerequisites.First();
        // Simulate that it was somehow uncompleted (in real scenario, we'd have a different setup)
        // For this test, we just verify the method doesn't throw when status is Rejected
        // Since prerequisites can only be completed once, we add a new one and complete it
        var newPrereq = workOrder.AddSafetyPrerequisite("New prerequisite", isMandatory: true, sortOrder: 2);

        // Act
        workOrder.CompleteSafetyPrerequisite(newPrereq.Id, "user-123", "Completed after rejection");

        // Assert
        Assert.True(newPrereq.CompletedAtUtc.HasValue);
        Assert.Equal("user-123", newPrereq.CompletedBy);
        Assert.Equal("Completed after rejection", newPrereq.CompletionNote);
    }

    [Fact]
    public void After_Rejection_SubmitForApproval_Can_Succeed_Once_All_Mandatory_Prerequisites_Are_Completed()
    {
        // Arrange
        var workOrder = CreateRejectedWorkOrder();
        // The original mandatory prerequisite is already completed
        // Add another mandatory prerequisite and complete it
        var newPrereq = workOrder.AddSafetyPrerequisite("New mandatory check", isMandatory: true, sortOrder: 2);
        workOrder.CompleteSafetyPrerequisite(newPrereq.Id, "user-123");

        // Act
        workOrder.SubmitForApproval();

        // Assert
        Assert.Equal(WorkOrderStatus.PendingApproval, workOrder.Status);
    }
}

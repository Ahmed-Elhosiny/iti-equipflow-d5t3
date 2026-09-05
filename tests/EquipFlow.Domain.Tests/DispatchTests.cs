using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class DispatchTests
{
    private WorkOrder CreateApprovedWorkOrder()
    {
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123");
        workOrder.SubmitForApproval();
        workOrder.Approve("approver-456");
        
        return workOrder;
    }

    [Fact]
    public void MarkDispatched_Throws_When_WorkOrder_Is_Draft()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");

        // Act & Assert
        var act = () => workOrder.MarkDispatched();
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void MarkDispatched_Throws_When_WorkOrder_Is_PendingApproval()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123");
        workOrder.SubmitForApproval();

        // Act & Assert
        var act = () => workOrder.MarkDispatched();
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void MarkDispatched_Throws_When_WorkOrder_Is_Rejected()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123");
        workOrder.SubmitForApproval();
        workOrder.Reject("approver-456");

        // Act & Assert
        var act = () => workOrder.MarkDispatched();
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void MarkDispatched_Succeeds_When_WorkOrder_Is_Approved()
    {
        // Arrange
        var workOrder = CreateApprovedWorkOrder();

        // Act
        workOrder.MarkDispatched();

        // Assert
        Assert.Equal(WorkOrderStatus.Dispatched, workOrder.Status);
    }

    [Fact]
    public void MarkDispatched_Sets_Status_To_Dispatched()
    {
        // Arrange
        var workOrder = CreateApprovedWorkOrder();

        // Act
        workOrder.MarkDispatched();

        // Assert
        Assert.Equal(WorkOrderStatus.Dispatched, workOrder.Status);
    }
}

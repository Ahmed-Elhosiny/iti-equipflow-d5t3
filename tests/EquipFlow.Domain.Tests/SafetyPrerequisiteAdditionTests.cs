using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class SafetyPrerequisiteAdditionTests
{
    private WorkOrder CreateDraftWorkOrder()
    {
        return new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
    }

    [Fact]
    public void AddSafetyPrerequisite_Adds_Prerequisite_When_WorkOrder_Is_Draft()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();

        // Act
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);

        // Assert
        Assert.Single(workOrder.SafetyPrerequisites);
        Assert.Equal("Wear safety goggles", prerequisite.Description);
        Assert.True(prerequisite.IsMandatory);
        Assert.Equal(1, prerequisite.SortOrder);
    }

    [Fact]
    public void AddSafetyPrerequisite_Throws_When_WorkOrder_Is_PendingApproval()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();
        workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(workOrder.SafetyPrerequisites.First().Id, "user-123");
        workOrder.SubmitForApproval();

        // Act & Assert
        var act = () => workOrder.AddSafetyPrerequisite("Another prerequisite", isMandatory: false, sortOrder: 2);
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void AddSafetyPrerequisite_Throws_When_WorkOrder_Is_Approved()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();
        workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(workOrder.SafetyPrerequisites.First().Id, "user-123");
        workOrder.SubmitForApproval();
        workOrder.Approve("approver-456");

        // Act & Assert
        var act = () => workOrder.AddSafetyPrerequisite("Another prerequisite", isMandatory: false, sortOrder: 2);
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void AddSafetyPrerequisite_Throws_When_WorkOrder_Is_Dispatched()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();
        workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(workOrder.SafetyPrerequisites.First().Id, "user-123");
        workOrder.SubmitForApproval();
        workOrder.Approve("approver-456");
        workOrder.MarkDispatched();

        // Act & Assert
        var act = () => workOrder.AddSafetyPrerequisite("Another prerequisite", isMandatory: false, sortOrder: 2);
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void AddSafetyPrerequisite_Allowed_When_WorkOrder_Is_Rejected()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();
        workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(workOrder.SafetyPrerequisites.First().Id, "user-123");
        workOrder.SubmitForApproval();
        workOrder.Reject("approver-456", "Need more information");

        // Act
        var prerequisite = workOrder.AddSafetyPrerequisite("Additional safety check", isMandatory: false, sortOrder: 2);

        // Assert
        Assert.Equal(2, workOrder.SafetyPrerequisites.Count);
        Assert.Equal("Additional safety check", prerequisite.Description);
    }
}

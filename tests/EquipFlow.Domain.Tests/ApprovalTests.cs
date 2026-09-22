using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class ApprovalTests
{
    private WorkOrder CreateSubmittedWorkOrder()
    {
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123");
        workOrder.SubmitForApproval("Test User");
        
        return workOrder;
    }

    [Fact]
    public void Approve_Throws_When_WorkOrder_Is_Not_PendingApproval()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");

        // Act & Assert
        var act = () => workOrder.Approve("approver-456");
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Approve_Succeeds_When_WorkOrder_Is_PendingApproval_And_Mandatory_Prerequisites_Completed()
    {
        // Arrange
        var workOrder = CreateSubmittedWorkOrder();

        // Act
        workOrder.Approve("approver-456", "Looks good");

        // Assert
        Assert.Equal(WorkOrderStatus.Approved, workOrder.Status);
        Assert.Equal("approver-456", workOrder.DecisionBy);
        Assert.NotNull(workOrder.DecisionAtUtc);
        Assert.Equal("Looks good", workOrder.DecisionComment);
    }

    [Fact]
    public void Approve_Sets_Status_To_Approved_And_Records_DecisionBy_And_DecisionAtUtc()
    {
        // Arrange
        var workOrder = CreateSubmittedWorkOrder();
        var beforeApprove = DateTimeOffset.UtcNow;

        // Act
        workOrder.Approve("approver-456");

        // Assert
        Assert.Equal(WorkOrderStatus.Approved, workOrder.Status);
        Assert.Equal("approver-456", workOrder.DecisionBy);
        Assert.NotNull(workOrder.DecisionAtUtc);
        Assert.True(workOrder.DecisionAtUtc >= beforeApprove.AddSeconds(-5));
    }

    [Fact]
    public void Reject_Succeeds_When_WorkOrder_Is_PendingApproval()
    {
        // Arrange
        var workOrder = CreateSubmittedWorkOrder();

        // Act
        workOrder.Reject("approver-456", "Missing information");

        // Assert
        Assert.Equal(WorkOrderStatus.Rejected, workOrder.Status);
        Assert.Equal("approver-456", workOrder.DecisionBy);
        Assert.NotNull(workOrder.DecisionAtUtc);
        Assert.Equal("Missing information", workOrder.DecisionComment);
    }

    [Fact]
    public void Reject_Sets_Status_To_Rejected_And_Records_DecisionBy_And_DecisionAtUtc()
    {
        // Arrange
        var workOrder = CreateSubmittedWorkOrder();

        // Act
        workOrder.Reject("approver-456");

        // Assert
        Assert.Equal(WorkOrderStatus.Rejected, workOrder.Status);
        Assert.Equal("approver-456", workOrder.DecisionBy);
        Assert.NotNull(workOrder.DecisionAtUtc);
    }

          [Fact]
    public void EditAndApprove_Throws_When_WorkOrder_Is_Not_PendingApproval()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");

        // Act & Assert
        var act = () => workOrder.EditAndApprove("approver-456", null, "New Title", null, null, null, null, null);
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void EditAndApprove_Succeeds_And_Updates_Fields_When_PendingApproval()
    {
        // Arrange
        var workOrder = CreateSubmittedWorkOrder();

        // Act
        workOrder.EditAndApprove(
            approverUserId: "approver-456",
            comment: "Updated priority",
            title: "Updated Title",
            symptom: "Updated Symptom",
            equipmentName: "Pump-102",
            equipmentAssetNumber: "AST-999",
            manualRevision: "Rev 2",
            location: "Building B");

        // Assert
        Assert.Equal(WorkOrderStatus.Approved, workOrder.Status);
        Assert.Equal("approver-456", workOrder.DecisionBy);
        Assert.NotNull(workOrder.DecisionAtUtc);
        Assert.Equal("Updated Title", workOrder.Title);
        Assert.Equal("Updated Symptom", workOrder.Symptom);
        Assert.Equal("Pump-102", workOrder.EquipmentName);
        Assert.Equal("AST-999", workOrder.EquipmentAssetNumber);
        Assert.Equal("Rev 2", workOrder.ManualRevision);
        Assert.Equal("Building B", workOrder.Location);
    }

    [Fact]
    public void EditAndApprove_Records_EditedAndApproved_ActionType_In_Audit_Log()
    {
        // Arrange
        var workOrder = CreateSubmittedWorkOrder();

        // Act
        workOrder.EditAndApprove("approver-456", "Minor edits", "New Title", null, null, null, null, null);

        // Assert
        var action = Assert.Single(workOrder.ApprovalActions.Where(a => a.ActionType == ApprovalActionType.EditedAndApproved));
        Assert.Equal("approver-456", action.ActorUserId);
        Assert.Contains("Edited: Title", action.Comment);
        Assert.Contains("Minor edits", action.Comment);
    }

    [Fact]
    public void EditAndApprove_Does_Not_Update_Fields_When_Null_Or_Empty_Provided()
    {
        // Arrange
        var workOrder = CreateSubmittedWorkOrder();
        var originalTitle = workOrder.Title;
        var originalSymptom = workOrder.Symptom;

        // Act
        workOrder.EditAndApprove("approver-456", null, null, null, null, null, null, null);

        // Assert
        Assert.Equal(WorkOrderStatus.Approved, workOrder.Status);
        Assert.Equal(originalTitle, workOrder.Title);
        Assert.Equal(originalSymptom, workOrder.Symptom);
    }  
}

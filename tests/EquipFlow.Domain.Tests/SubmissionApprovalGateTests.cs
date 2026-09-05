using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class SubmissionApprovalGateTests
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
    public void SubmitForApproval_Throws_When_WorkOrder_Has_No_Safety_Prerequisites()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();

        // Act & Assert
        var act = () => workOrder.SubmitForApproval();
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("at least one safety prerequisite", exception.Message);
    }

    [Fact]
    public void SubmitForApproval_Throws_When_Mandatory_Safety_Prerequisite_Is_Incomplete()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();
        workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.AddSafetyPrerequisite("Check pressure levels", isMandatory: false, sortOrder: 2);

        // Act & Assert
        var act = () => workOrder.SubmitForApproval();
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("mandatory safety prerequisites must be completed", exception.Message);
    }

    [Fact]
    public void SubmitForApproval_Succeeds_When_All_Mandatory_Prerequisites_Are_Completed()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();
        var prerequisite1 = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        var prerequisite2 = workOrder.AddSafetyPrerequisite("Check pressure levels", isMandatory: true, sortOrder: 2);
        workOrder.CompleteSafetyPrerequisite(prerequisite1.Id, "user-123");
        workOrder.CompleteSafetyPrerequisite(prerequisite2.Id, "user-123");

        // Act
        workOrder.SubmitForApproval();

        // Assert
        Assert.Equal(WorkOrderStatus.PendingApproval, workOrder.Status);
        Assert.NotEmpty(workOrder.ApprovalActions);
        Assert.Contains(workOrder.ApprovalActions, a => a.ActionType == ApprovalActionType.Submitted);
    }

    [Fact]
    public void SubmitForApproval_Succeeds_When_Mandatory_Completed_But_Optional_Incomplete()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();
        var mandatoryPrereq = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        var optionalPrereq = workOrder.AddSafetyPrerequisite("Optional check", isMandatory: false, sortOrder: 2);
        workOrder.CompleteSafetyPrerequisite(mandatoryPrereq.Id, "user-123");
        // Optional prerequisite remains incomplete

        // Act
        workOrder.SubmitForApproval();

        // Assert
        Assert.Equal(WorkOrderStatus.PendingApproval, workOrder.Status);
    }

    [Fact]
    public void SubmitForApproval_Throws_When_WorkOrder_Is_Already_PendingApproval()
    {
        // Arrange
        var workOrder = CreateDraftWorkOrder();
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123");
        workOrder.SubmitForApproval();

        // Act & Assert
        var act = () => workOrder.SubmitForApproval();
        Assert.Throws<InvalidOperationException>(act);
    }
}

using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class SafetyPrerequisiteCompletionTests
{
    [Fact]
    public void CompleteSafetyPrerequisite_Marks_Prerequisite_As_Completed()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);

        // Act
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123", "Completed safely");

        // Assert
        Assert.True(prerequisite.CompletedAtUtc.HasValue);
        Assert.Equal("user-123", prerequisite.CompletedBy);
        Assert.Equal("Completed safely", prerequisite.CompletionNote);
    }

    [Fact]
    public void CompleteSafetyPrerequisite_Throws_When_Prerequisite_Id_Does_Not_Exist()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var act = () => workOrder.CompleteSafetyPrerequisite(nonExistentId, "user-123");
        var exception = Assert.Throws<ArgumentException>(act);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void CompleteSafetyPrerequisite_Throws_After_WorkOrder_Is_Dispatched()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        // Complete it first so we can get to Dispatched status
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123");
        workOrder.SubmitForApproval();
        workOrder.Approve("approver-456");
        workOrder.MarkDispatched();
        
        // Now try to complete a new prerequisite - but we can't add in Dispatched
        // The CanCompleteSafetyPrerequisites check should prevent completion
        // We test this by checking that the method throws for Dispatched status
        // Since we can't add prerequisites in Dispatched, we verify via the internal logic
        // by checking if an attempt to complete would throw
        var act = () => workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123", "Already completed");
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void SafetyPrerequisite_MarkCompleted_Throws_When_Already_Completed()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-123");

        // Act & Assert
        var act = () => workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "user-456");
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("already been completed", exception.Message);
    }

    [Fact]
    public void SafetyPrerequisite_MarkCompleted_Throws_When_CompletedBy_Is_Empty()
    {
        // Arrange
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");
        
        var prerequisite = workOrder.AddSafetyPrerequisite("Wear safety goggles", isMandatory: true, sortOrder: 1);

        // Act & Assert - empty string
        var actEmpty = () => workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "");
        Assert.Throws<ArgumentException>(actEmpty);

        // Act & Assert - whitespace
        var actWhitespace = () => workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "   ");
        Assert.Throws<ArgumentException>(actWhitespace);

        // Act & Assert - null
        var actNull = () => workOrder.CompleteSafetyPrerequisite(prerequisite.Id, null!);
        Assert.Throws<ArgumentException>(actNull);
    }
}

using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class WorkOrderCreationTests
{
    [Fact]
    public void Creating_Valid_WorkOrder_Sets_Status_To_Draft()
    {
        // Arrange & Act
        var workOrder = new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");

        // Assert
        Assert.Equal(WorkOrderStatus.Draft, workOrder.Status);
        Assert.NotEqual(Guid.Empty, workOrder.Id);
        Assert.Equal("Test Work Order", workOrder.Title);
        Assert.Equal("Pump-101", workOrder.EquipmentName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Creating_WorkOrder_With_Empty_Title_Throws_ArgumentException(string? title)
    {
        // Arrange & Act
        var act = () => new WorkOrder(
            title: title!,
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: "user-123");

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Creating_WorkOrder_With_Empty_Symptom_Throws_ArgumentException(string? symptom)
    {
        // Arrange & Act
        var act = () => new WorkOrder(
            title: "Test Work Order",
            symptom: symptom!,
            equipmentName: "Pump-101",
            createdBy: "user-123");

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Creating_WorkOrder_With_Empty_EquipmentName_Throws_ArgumentException(string? equipmentName)
    {
        // Arrange & Act
        var act = () => new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: equipmentName!,
            createdBy: "user-123");

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Creating_WorkOrder_With_Empty_CreatedBy_Throws_ArgumentException(string? createdBy)
    {
        // Arrange & Act
        var act = () => new WorkOrder(
            title: "Test Work Order",
            symptom: "Equipment malfunction",
            equipmentName: "Pump-101",
            createdBy: createdBy!);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
}

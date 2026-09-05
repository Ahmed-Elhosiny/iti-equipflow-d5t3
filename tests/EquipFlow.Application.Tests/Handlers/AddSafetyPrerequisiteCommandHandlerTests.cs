using EquipFlow.Application.Common;
using EquipFlow.Application.Tests.Fakes;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Handlers;

namespace EquipFlow.Application.Tests.Handlers;

public class AddSafetyPrerequisiteCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_AddsSafetyPrerequisiteToExistingWorkOrder()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new AddSafetyPrerequisiteCommandHandler(repository);
        
        // First create a work order
        var createHandler = new CreateWorkOrderCommandHandler(repository);
        var createCommand = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");
        var workOrderId = await createHandler.HandleAsync(createCommand);

        var command = new AddSafetyPrerequisiteCommand(
            workOrderId,
            "Wear safety goggles",
            true,
            1);

        // Act
        await handler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        Assert.Single(savedWorkOrder.SafetyPrerequisites);
        var prerequisite = savedWorkOrder.SafetyPrerequisites.First();
        Assert.Equal("Wear safety goggles", prerequisite.Description);
        Assert.True(prerequisite.IsMandatory);
        Assert.Equal(1, prerequisite.SortOrder);
    }

    [Fact]
    public async Task HandleAsync_ThrowsWorkOrderNotFoundException_WhenWorkOrderDoesNotExist()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new AddSafetyPrerequisiteCommandHandler(repository);
        var command = new AddSafetyPrerequisiteCommand(
            Guid.NewGuid(),
            "Wear safety goggles",
            true,
            1);

        // Act & Assert
        await Assert.ThrowsAsync<WorkOrderNotFoundException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_CallsSaveChangesAsync()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new AddSafetyPrerequisiteCommandHandler(repository);
        
        // First create a work order
        var createHandler = new CreateWorkOrderCommandHandler(repository);
        var createCommand = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");
        var workOrderId = await createHandler.HandleAsync(createCommand);

        var command = new AddSafetyPrerequisiteCommand(
            workOrderId,
            "Wear safety goggles",
            true,
            1);

        // Act
        await handler.HandleAsync(command);

        // Assert
        Assert.True(repository.SaveChangesCalled);
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenDescriptionIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new AddSafetyPrerequisiteCommandHandler(repository);
        var command = new AddSafetyPrerequisiteCommand(
            Guid.NewGuid(),
            "",
            true,
            1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }
}

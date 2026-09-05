using EquipFlow.Application.Common;
using EquipFlow.Application.Tests.Fakes;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Handlers;

namespace EquipFlow.Application.Tests.Handlers;

public class CompleteSafetyPrerequisiteCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_CompletesExistingSafetyPrerequisite()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var completeHandler = new CompleteSafetyPrerequisiteCommandHandler(repository);
        
        // First create a work order with a safety prerequisite
        var createHandler = new CreateWorkOrderCommandHandler(repository);
        var createCommand = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");
        var workOrderId = await createHandler.HandleAsync(createCommand);

        var addHandler = new AddSafetyPrerequisiteCommandHandler(repository);
        var addCommand = new AddSafetyPrerequisiteCommand(
            workOrderId,
            "Wear safety goggles",
            true,
            1);
        await addHandler.HandleAsync(addCommand);

        var workOrder = repository.GetSavedWorkOrder(workOrderId);
        var prerequisiteId = workOrder!.SafetyPrerequisites.First().Id;

        var command = new CompleteSafetyPrerequisiteCommand(
            workOrderId,
            prerequisiteId,
            "Test User",
            "Completed successfully");

        // Act
        await completeHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        var completedPrerequisite = savedWorkOrder.SafetyPrerequisites.First();
        Assert.True(completedPrerequisite.CompletedAtUtc.HasValue);
        Assert.Equal("Test User", completedPrerequisite.CompletedBy);
        Assert.Equal("Completed successfully", completedPrerequisite.CompletionNote);
    }

    [Fact]
    public async Task HandleAsync_ThrowsWorkOrderNotFoundException_WhenWorkOrderDoesNotExist()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CompleteSafetyPrerequisiteCommandHandler(repository);
        var command = new CompleteSafetyPrerequisiteCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test User",
            null);

        // Act & Assert
        await Assert.ThrowsAsync<WorkOrderNotFoundException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_PropagatesDomainError_WhenPrerequisiteIdDoesNotExist()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CompleteSafetyPrerequisiteCommandHandler(repository);
        
        // First create a work order without prerequisites
        var createHandler = new CreateWorkOrderCommandHandler(repository);
        var createCommand = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");
        var workOrderId = await createHandler.HandleAsync(createCommand);

        var command = new CompleteSafetyPrerequisiteCommand(
            workOrderId,
            Guid.NewGuid(), // Non-existent prerequisite id
            "Test User",
            null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_CallsSaveChangesAsync()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var completeHandler = new CompleteSafetyPrerequisiteCommandHandler(repository);
        
        // First create a work order with a safety prerequisite
        var createHandler = new CreateWorkOrderCommandHandler(repository);
        var createCommand = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");
        var workOrderId = await createHandler.HandleAsync(createCommand);

        var addHandler = new AddSafetyPrerequisiteCommandHandler(repository);
        var addCommand = new AddSafetyPrerequisiteCommand(
            workOrderId,
            "Wear safety goggles",
            true,
            1);
        await addHandler.HandleAsync(addCommand);

        var workOrder = repository.GetSavedWorkOrder(workOrderId);
        var prerequisiteId = workOrder!.SafetyPrerequisites.First().Id;

        var command = new CompleteSafetyPrerequisiteCommand(
            workOrderId,
            prerequisiteId,
            "Test User",
            null);

        // Act
        await completeHandler.HandleAsync(command);

        // Assert
        Assert.True(repository.SaveChangesCalled);
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenCompletedByIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CompleteSafetyPrerequisiteCommandHandler(repository);
        var command = new CompleteSafetyPrerequisiteCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "",
            null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }
}

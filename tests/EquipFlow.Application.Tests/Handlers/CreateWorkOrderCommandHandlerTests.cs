using EquipFlow.Application.Tests.Fakes;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Handlers;

namespace EquipFlow.Application.Tests.Handlers;

public class CreateWorkOrderCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesDraftWorkOrderAndReturnsId()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CreateWorkOrderCommandHandler(repository);
        var command = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        var savedWorkOrder = repository.GetSavedWorkOrder(result);
        Assert.NotNull(savedWorkOrder);
        Assert.Equal("Test Title", savedWorkOrder.Title);
        Assert.Equal(Domain.Enums.WorkOrderStatus.Draft, savedWorkOrder.Status);
    }

    [Fact]
    public async Task HandleAsync_CallsSaveChangesAsync()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CreateWorkOrderCommandHandler(repository);
        var command = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");

        // Act
        await handler.HandleAsync(command);

        // Assert
        Assert.True(repository.SaveChangesCalled);
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenTitleIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CreateWorkOrderCommandHandler(repository);
        var command = new CreateWorkOrderCommand(
            "",
            "Test Symptom",
            "Test Equipment",
            "Test User");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenSymptomIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CreateWorkOrderCommandHandler(repository);
        var command = new CreateWorkOrderCommand(
            "Test Title",
            "",
            "Test Equipment",
            "Test User");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenEquipmentNameIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CreateWorkOrderCommandHandler(repository);
        var command = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "",
            "Test User");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenCreatedByIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new CreateWorkOrderCommandHandler(repository);
        var command = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }
}

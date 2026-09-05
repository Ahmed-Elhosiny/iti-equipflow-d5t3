using EquipFlow.Application.Common;
using EquipFlow.Application.Tests.Fakes;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Handlers;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Application.Tests.Handlers;

public class SubmitWorkOrderForApprovalCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ThrowsWorkOrderNotFoundException_WhenWorkOrderDoesNotExist()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        var command = new SubmitWorkOrderForApprovalCommand(
            Guid.NewGuid(),
            "Test User");

        // Act & Assert
        await Assert.ThrowsAsync<WorkOrderNotFoundException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_PropagatesDomainError_WhenNoSafetyPrerequisites()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        
        // First create a work order without prerequisites
        var createHandler = new CreateWorkOrderCommandHandler(repository);
        var createCommand = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");
        var workOrderId = await createHandler.HandleAsync(createCommand);

        var command = new SubmitWorkOrderForApprovalCommand(
            workOrderId,
            "Test User");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_PropagatesDomainError_WhenMandatoryPrerequisiteIncomplete()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        
        // First create a work order with an incomplete mandatory prerequisite
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
            true, // Mandatory
            1);
        await addHandler.HandleAsync(addCommand);

        var command = new SubmitWorkOrderForApprovalCommand(
            workOrderId,
            "Test User");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_SubmitsSuccessfully_WhenMandatoryPrerequisitesCompleted()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var submitHandler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        
        // First create a work order with a completed mandatory prerequisite
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

        var completeHandler = new CompleteSafetyPrerequisiteCommandHandler(repository);
        var completeCommand = new CompleteSafetyPrerequisiteCommand(
            workOrderId,
            prerequisiteId,
            "Test User",
            null);
        await completeHandler.HandleAsync(completeCommand);

        var command = new SubmitWorkOrderForApprovalCommand(
            workOrderId,
            "Test User");

        // Act
        await submitHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        Assert.Equal(WorkOrderStatus.PendingApproval, savedWorkOrder.Status);
        Assert.Contains(savedWorkOrder.ApprovalActions, a => a.ActionType == ApprovalActionType.Submitted);
    }

    [Fact]
    public async Task HandleAsync_RecordsApprovalActionTypeSubmitted()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var submitHandler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        
        // First create a work order with a completed mandatory prerequisite
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

        var completeHandler = new CompleteSafetyPrerequisiteCommandHandler(repository);
        var completeCommand = new CompleteSafetyPrerequisiteCommand(
            workOrderId,
            prerequisiteId,
            "Test User",
            null);
        await completeHandler.HandleAsync(completeCommand);

        var command = new SubmitWorkOrderForApprovalCommand(
            workOrderId,
            "Submitter User");

        // Act
        await submitHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        var submittedAction = Assert.Single(savedWorkOrder.ApprovalActions);
        Assert.Equal(ApprovalActionType.Submitted, submittedAction.ActionType);
        Assert.Equal("Submitter User", submittedAction.ActorUserId);
    }

    [Fact]
    public async Task HandleAsync_CallsSaveChangesAsync()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var submitHandler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        
        // First create a work order with a completed mandatory prerequisite
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

        var completeHandler = new CompleteSafetyPrerequisiteCommandHandler(repository);
        var completeCommand = new CompleteSafetyPrerequisiteCommand(
            workOrderId,
            prerequisiteId,
            "Test User",
            null);
        await completeHandler.HandleAsync(completeCommand);

        var command = new SubmitWorkOrderForApprovalCommand(
            workOrderId,
            "Test User");

        // Act
        await submitHandler.HandleAsync(command);

        // Assert
        Assert.True(repository.SaveChangesCalled);
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenSubmittedByIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        var command = new SubmitWorkOrderForApprovalCommand(
            Guid.NewGuid(),
            "");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }
}

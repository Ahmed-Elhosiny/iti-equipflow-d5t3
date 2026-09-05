using EquipFlow.Application.Common;
using EquipFlow.Application.Tests.Fakes;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Handlers;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Application.Tests.Handlers;

public class DispatchWorkOrderCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ThrowsWorkOrderNotFoundException_WhenWorkOrderDoesNotExist()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new DispatchWorkOrderCommandHandler(repository);
        var command = new DispatchWorkOrderCommand(
            Guid.NewGuid(),
            "Test User");

        // Act & Assert
        await Assert.ThrowsAsync<WorkOrderNotFoundException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_DispatchesApprovedWorkOrder()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var dispatchHandler = new DispatchWorkOrderCommandHandler(repository);
        
        // First create a work order, submit it, and approve it
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

        var submitHandler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        var submitCommand = new SubmitWorkOrderForApprovalCommand(
            workOrderId,
            "Test User");
        await submitHandler.HandleAsync(submitCommand);

        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        var reviewCommand = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Approve,
            "Approver User",
            null);
        await reviewHandler.HandleAsync(reviewCommand);

        var command = new DispatchWorkOrderCommand(
            workOrderId,
            "Dispatcher User");

        // Act
        await dispatchHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        Assert.Equal(WorkOrderStatus.Dispatched, savedWorkOrder.Status);
    }

    [Fact]
    public async Task HandleAsync_RecordsApprovalActionTypeDispatched()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var dispatchHandler = new DispatchWorkOrderCommandHandler(repository);
        
        // First create a work order, submit it, and approve it
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

        var submitHandler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        var submitCommand = new SubmitWorkOrderForApprovalCommand(
            workOrderId,
            "Test User");
        await submitHandler.HandleAsync(submitCommand);

        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        var reviewCommand = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Approve,
            "Approver User",
            null);
        await reviewHandler.HandleAsync(reviewCommand);

        var command = new DispatchWorkOrderCommand(
            workOrderId,
            "Dispatcher User");

        // Act
        await dispatchHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        var dispatchedAction = Assert.Single(savedWorkOrder.ApprovalActions.Where(a => a.ActionType == ApprovalActionType.Dispatched));
        Assert.NotNull(dispatchedAction);
    }

    [Fact]
    public async Task HandleAsync_PropagatesDomainError_WhenWorkOrderNotApproved()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var dispatchHandler = new DispatchWorkOrderCommandHandler(repository);
        
        // First create a work order but don't submit or approve it (stays in Draft)
        var createHandler = new CreateWorkOrderCommandHandler(repository);
        var createCommand = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");
        var workOrderId = await createHandler.HandleAsync(createCommand);

        var command = new DispatchWorkOrderCommand(
            workOrderId,
            "Dispatcher User");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatchHandler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_CallsSaveChangesAsync()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var dispatchHandler = new DispatchWorkOrderCommandHandler(repository);
        
        // First create a work order, submit it, and approve it
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

        var submitHandler = new SubmitWorkOrderForApprovalCommandHandler(repository);
        var submitCommand = new SubmitWorkOrderForApprovalCommand(
            workOrderId,
            "Test User");
        await submitHandler.HandleAsync(submitCommand);

        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        var reviewCommand = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Approve,
            "Approver User",
            null);
        await reviewHandler.HandleAsync(reviewCommand);

        var command = new DispatchWorkOrderCommand(
            workOrderId,
            "Dispatcher User");

        // Act
        await dispatchHandler.HandleAsync(command);

        // Assert
        Assert.True(repository.SaveChangesCalled);
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenDispatcherUserIdIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new DispatchWorkOrderCommandHandler(repository);
        var command = new DispatchWorkOrderCommand(
            Guid.NewGuid(),
            "");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }
}

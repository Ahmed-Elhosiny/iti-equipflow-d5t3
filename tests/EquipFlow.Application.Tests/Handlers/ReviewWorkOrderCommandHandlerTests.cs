using EquipFlow.Application.Common;
using EquipFlow.Application.Tests.Fakes;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Handlers;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Application.Tests.Handlers;

public class ReviewWorkOrderCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ThrowsWorkOrderNotFoundException_WhenWorkOrderDoesNotExist()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new ReviewWorkOrderCommandHandler(repository);
        var command = new ReviewWorkOrderCommand(
            Guid.NewGuid(),
            WorkOrderReviewDecision.Approve,
            "Test User",
            null);

        // Act & Assert
        await Assert.ThrowsAsync<WorkOrderNotFoundException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_ApprovesPendingApprovalWorkOrder()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        
        // First create a work order and submit it for approval
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

        var command = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Approve,
            "Approver User",
            "Looks good");

        // Act
        await reviewHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        Assert.Equal(WorkOrderStatus.Approved, savedWorkOrder.Status);
        Assert.Equal("Approver User", savedWorkOrder.DecisionBy);
        Assert.Equal("Looks good", savedWorkOrder.DecisionComment);
    }

    [Fact]
    public async Task HandleAsync_RecordsApprovalActionTypeApproved()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        
        // First create a work order and submit it for approval
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

        var command = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Approve,
            "Approver User",
            null);

        // Act
        await reviewHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        var approvedAction = Assert.Single(savedWorkOrder.ApprovalActions.Where(a => a.ActionType == ApprovalActionType.Approved));
        Assert.Equal("Approver User", approvedAction.ActorUserId);
    }

    [Fact]
    public async Task HandleAsync_RejectsPendingApprovalWorkOrder()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        
        // First create a work order and submit it for approval
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

        var command = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Reject,
            "Reviewer User",
            "Needs more work");

        // Act
        await reviewHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        Assert.Equal(WorkOrderStatus.Rejected, savedWorkOrder.Status);
        Assert.Equal("Reviewer User", savedWorkOrder.DecisionBy);
        Assert.Equal("Needs more work", savedWorkOrder.DecisionComment);
    }

    [Fact]
    public async Task HandleAsync_RecordsApprovalActionTypeRejected()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        
        // First create a work order and submit it for approval
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

        var command = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Reject,
            "Reviewer User",
            null);

        // Act
        await reviewHandler.HandleAsync(command);

        // Assert
        var savedWorkOrder = repository.GetSavedWorkOrder(workOrderId);
        Assert.NotNull(savedWorkOrder);
        var rejectedAction = Assert.Single(savedWorkOrder.ApprovalActions.Where(a => a.ActionType == ApprovalActionType.Rejected));
        Assert.Equal("Reviewer User", rejectedAction.ActorUserId);
    }

    [Fact]
    public async Task HandleAsync_PropagatesDomainError_WhenWorkOrderNotPendingApproval()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        
        // First create a work order but don't submit it (it stays in Draft)
        var createHandler = new CreateWorkOrderCommandHandler(repository);
        var createCommand = new CreateWorkOrderCommand(
            "Test Title",
            "Test Symptom",
            "Test Equipment",
            "Test User");
        var workOrderId = await createHandler.HandleAsync(createCommand);

        var command = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Approve,
            "Approver User",
            null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => reviewHandler.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_CallsSaveChangesAsync()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var reviewHandler = new ReviewWorkOrderCommandHandler(repository);
        
        // First create a work order and submit it for approval
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

        var command = new ReviewWorkOrderCommand(
            workOrderId,
            WorkOrderReviewDecision.Approve,
            "Approver User",
            null);

        // Act
        await reviewHandler.HandleAsync(command);

        // Assert
        Assert.True(repository.SaveChangesCalled);
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentException_WhenReviewerUserIdIsEmpty()
    {
        // Arrange
        var repository = new FakeWorkOrderRepository();
        var handler = new ReviewWorkOrderCommandHandler(repository);
        var command = new ReviewWorkOrderCommand(
            Guid.NewGuid(),
            WorkOrderReviewDecision.Approve,
            "",
            null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }
}

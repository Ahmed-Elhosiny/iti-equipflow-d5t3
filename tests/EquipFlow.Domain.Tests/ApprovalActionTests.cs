using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class ApprovalActionTests
{
    [Fact]
    public void Creating_ApprovalAction_With_Empty_WorkOrderId_Throws_ArgumentException()
    {
        // Arrange & Act
        var act = () => new ApprovalAction(
            workOrderId: Guid.Empty,
            actionType: ApprovalActionType.Submitted,
            actorUserId: "user-123");

        // Assert
        var exception = Assert.Throws<ArgumentException>(act);
        Assert.Contains("WorkOrderId cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Creating_ApprovalAction_With_Empty_ActorUserId_Throws_ArgumentException(string? actorUserId)
    {
        // Arrange & Act
        var act = () => new ApprovalAction(
            workOrderId: Guid.NewGuid(),
            actionType: ApprovalActionType.Submitted,
            actorUserId: actorUserId!);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Creating_Valid_ApprovalAction_Stores_ActionType_ActorUserId_Comment_And_OccurredAtUtc()
    {
        // Arrange
        var workOrderId = Guid.NewGuid();
        var actorUserId = "user-123";
        var comment = "Initial submission";
        var beforeCreate = DateTimeOffset.UtcNow;

        // Act
        var approvalAction = new ApprovalAction(
            workOrderId: workOrderId,
            actionType: ApprovalActionType.Submitted,
            actorUserId: actorUserId,
            comment: comment);

        // Assert
        Assert.NotEqual(Guid.Empty, approvalAction.Id);
        Assert.Equal(workOrderId, approvalAction.WorkOrderId);
        Assert.Equal(ApprovalActionType.Submitted, approvalAction.ActionType);
        Assert.Equal(actorUserId, approvalAction.ActorUserId);
        Assert.Equal(comment, approvalAction.Comment);
        Assert.True(approvalAction.OccurredAtUtc >= beforeCreate.AddSeconds(-5));
    }

    [Fact]
    public void Creating_ApprovalAction_With_Null_Comment_Is_Valid()
    {
        // Arrange & Act
        var approvalAction = new ApprovalAction(
            workOrderId: Guid.NewGuid(),
            actionType: ApprovalActionType.Approved,
            actorUserId: "approver-456",
            comment: null);

        // Assert
        Assert.Null(approvalAction.Comment);
    }
}

using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Infrastructure.Tools.Executors;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EquipFlow.Infrastructure.Tests.Tools;

public sealed class ValidateBudgetExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenBudgetIsReserved_ReturnsApprovedResponse()
    {
        var governor = Substitute.For<ICostGovernor>();
        var reservation = CostGovernorResult.Reserved(
            Guid.NewGuid(),
            "primary",
            0.036m,
            9.964m);
        governor.EstimateAndReserveAsync(
                Arg.Any<string>(),
                3000,
                Arg.Any<decimal>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(reservation);
        var executor = CreateExecutor(governor);

        var result = await executor.ExecuteAsync(CreateRequest(
            JsonSerializer.Serialize(new ValidateBudgetRequest(Guid.NewGuid(), EstimatedTokens: 3000))));

        Assert.True(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.Success, result.Status);
        var response = JsonSerializer.Deserialize<ValidateBudgetResponse>(result.ResultJson!);
        Assert.NotNull(response);
        Assert.True(response.IsApproved);
        Assert.Equal(0.036m, response.ReservedAmount);
        Assert.Null(response.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBudgetIsInsufficient_ReturnsDeniedResponse()
    {
        var governor = Substitute.For<ICostGovernor>();
        governor.EstimateAndReserveAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<decimal>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(CostGovernorResult.Blocked(0.036m, 0.01m));
        var executor = CreateExecutor(governor);

        var result = await executor.ExecuteAsync(CreateRequest(
            JsonSerializer.Serialize(new ValidateBudgetRequest(Guid.NewGuid(), EstimatedTokens: 3000))));

        Assert.True(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.Success, result.Status);
        var response = JsonSerializer.Deserialize<ValidateBudgetResponse>(result.ResultJson!);
        Assert.NotNull(response);
        Assert.False(response.IsApproved);
        Assert.Equal(0, response.ReservedAmount);
        Assert.Equal("budget_exhausted", response.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGovernorThrows_ReturnsExecutorFailed()
    {
        var governor = Substitute.For<ICostGovernor>();
        governor.EstimateAndReserveAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<decimal>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(Task.FromException<CostGovernorResult>(
                new InvalidOperationException("Governor unavailable.")));
        var executor = CreateExecutor(governor);

        var result = await executor.ExecuteAsync(CreateRequest(
            JsonSerializer.Serialize(new ValidateBudgetRequest(Guid.NewGuid(), EstimatedTokens: 3000))));

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("VALIDATE_BUDGET_FAILED", result.ErrorCode);
        Assert.Equal("Governor unavailable.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJsonIsInvalid_ReturnsExecutorFailed()
    {
        var executor = CreateExecutor(Substitute.For<ICostGovernor>());

        var result = await executor.ExecuteAsync(CreateRequest("{ invalid json"));

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("VALIDATE_BUDGET_FAILED", result.ErrorCode);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static ValidateBudgetExecutor CreateExecutor(ICostGovernor governor) =>
        new(governor, Substitute.For<ILogger<ValidateBudgetExecutor>>());

    private static ToolInvocationRequest CreateRequest(string argumentsJson) =>
        new("ValidateBudget", argumentsJson, new ToolInvocationContext(
            Guid.NewGuid(),
            "Technician",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WorkOrderGenerator",
            Guid.NewGuid().ToString()));
}

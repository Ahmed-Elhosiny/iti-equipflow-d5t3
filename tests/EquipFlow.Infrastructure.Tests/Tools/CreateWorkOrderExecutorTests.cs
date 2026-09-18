using System.Text.Json;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Infrastructure.Tools.Executors;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EquipFlow.Infrastructure.Tests.Tools;

public sealed class CreateWorkOrderExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCommandSucceeds_ReturnsCreatedWorkOrder()
    {
        var sender = Substitute.For<ISender>();
        var workOrderId = Guid.NewGuid();
        sender.Send(Arg.Any<CreateWorkOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(workOrderId);
        var executor = CreateExecutor(sender);
        var request = CreateRequest(CreateArguments());

        var result = await executor.ExecuteAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.Success, result.Status);
        var response = JsonSerializer.Deserialize<CreateWorkOrderResponse>(result.ResultJson!);
        Assert.NotNull(response);
        Assert.Equal(workOrderId, response.WorkOrderId);
        await sender.Received(1).Send(
            Arg.Is<CreateWorkOrderCommand>(command =>
                command.Title == "Replace bearing"
                && command.Symptom == "Bearing is noisy"
                && command.CreatedBy == request.Context.UserId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMediatRThrows_ReturnsExecutorFailed()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<object?>(new InvalidOperationException("Handler unavailable.")));
        var executor = CreateExecutor(sender);

        var result = await executor.ExecuteAsync(CreateRequest(CreateArguments()));

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("CreateWorkOrder_FAILED", result.ErrorCode);
        Assert.Equal("Handler unavailable.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJsonIsInvalid_ReturnsExecutorFailed()
    {
        var executor = CreateExecutor(Substitute.For<ISender>());

        var result = await executor.ExecuteAsync(CreateRequest("{ invalid json"));

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("CreateWorkOrder_FAILED", result.ErrorCode);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static CreateWorkOrderExecutor CreateExecutor(ISender sender) =>
        new(sender, Substitute.For<ILogger<CreateWorkOrderExecutor>>());

    private static ToolInvocationRequest CreateRequest(string argumentsJson) =>
        new("CreateWorkOrder", argumentsJson, new ToolInvocationContext(
            Guid.NewGuid(),
            "Technician",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WorkOrderGenerator",
            Guid.NewGuid().ToString()));

    private static string CreateArguments() => JsonSerializer.Serialize(new CreateWorkOrderRequest(
        Guid.NewGuid(),
        "Replace bearing",
        "Bearing is noisy",
        125m,
        [],
        []));
}
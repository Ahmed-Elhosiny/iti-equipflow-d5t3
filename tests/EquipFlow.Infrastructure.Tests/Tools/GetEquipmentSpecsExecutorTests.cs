using System.Text.Json;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Domain;
using EquipFlow.Infrastructure.Tools.Executors;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EquipFlow.Infrastructure.Tests.Tools;

public sealed class GetEquipmentSpecsExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsEquipment_MapsNameAndSerialNumber()
    {
        var repository = Substitute.For<IEquipmentRepository>();
        var logger = Substitute.For<ILogger<GetEquipmentSpecsExecutor>>();
        var equipment = new Equipment
        {
            Name = "CNC Machine",
            SerialNumber = "SN-2000"
        };
        repository.GetByIdAsync(equipment.Id, Arg.Any<CancellationToken>())
            .Returns(equipment);
        var executor = new GetEquipmentSpecsExecutor(repository, logger);

        var result = await executor.ExecuteAsync(CreateRequest(equipment.Id));

        Assert.True(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.Success, result.Status);
        var response = JsonSerializer.Deserialize<GetEquipmentSpecsResponse>(result.ResultJson!);
        Assert.Equal(equipment.Name, response!.EquipmentName);
        Assert.Equal(equipment.SerialNumber, response.Model);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsNull_LogsWarningAndReturnsNotFoundResponse()
    {
        var repository = Substitute.For<IEquipmentRepository>();
        var logger = Substitute.For<ILogger<GetEquipmentSpecsExecutor>>();
        var equipmentId = Guid.NewGuid();
        repository.GetByIdAsync(equipmentId, Arg.Any<CancellationToken>())
            .Returns((Equipment?)null);
        var executor = new GetEquipmentSpecsExecutor(repository, logger);

        var result = await executor.ExecuteAsync(CreateRequest(equipmentId));

        Assert.True(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.Success, result.Status);
        var response = JsonSerializer.Deserialize<GetEquipmentSpecsResponse>(result.ResultJson!);
        Assert.Equal("Equipment not found", response!.EquipmentName);
        Assert.Equal(1, WarningLogCount(logger));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ReturnsExecutorFailedAndLogsWarning()
    {
        var repository = Substitute.For<IEquipmentRepository>();
        var logger = Substitute.For<ILogger<GetEquipmentSpecsExecutor>>();
        var exception = new InvalidOperationException("Repository unavailable.");
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Equipment?>(exception));
        var executor = new GetEquipmentSpecsExecutor(repository, logger);

        var result = await executor.ExecuteAsync(CreateRequest(Guid.NewGuid()));

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("GET_EQUIPMENT_SPECS_FAILED", result.ErrorCode);
        Assert.Equal(exception.Message, result.ErrorMessage);
        Assert.Equal(1, WarningLogCount(logger));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArgumentsAreInvalidJson_ReturnsExecutorFailedWithJsonExceptionMessage()
    {
        var logger = Substitute.For<ILogger<GetEquipmentSpecsExecutor>>();
        var executor = new GetEquipmentSpecsExecutor(
            Substitute.For<IEquipmentRepository>(),
            logger);
        var request = new ToolInvocationRequest(
            "GetEquipmentSpecs",
            "{ invalid json",
            CreateContext());

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("GET_EQUIPMENT_SPECS_FAILED", result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static ToolInvocationRequest CreateRequest(Guid equipmentId) =>
        new(
            "GetEquipmentSpecs",
            JsonSerializer.Serialize(new GetEquipmentSpecsRequest(equipmentId)),
            CreateContext());

    private static ToolInvocationContext CreateContext() => new(
        Guid.NewGuid(),
        "Technician",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "DiagnosticSafetyPlanner",
        Guid.NewGuid().ToString());

    private static int WarningLogCount(ILogger<GetEquipmentSpecsExecutor> logger) =>
        logger.ReceivedCalls().Count(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            call.GetArguments()[0] is LogLevel.Warning);
}

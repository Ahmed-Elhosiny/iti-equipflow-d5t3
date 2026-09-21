using System.Text.Json;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;
using EquipFlow.Infrastructure.Tools.Executors;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EquipFlow.Infrastructure.Tests.Tools;

public sealed class QueryFaultHistoryExecutorTests
{
    private const string EquipmentName = "Pump-101";

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsMatchingWorkOrder_MapsHistoryRecord()
    {
        var repository = Substitute.For<IWorkOrderRepository>();
        var logger = Substitute.For<ILogger<QueryFaultHistoryExecutor>>();
        var workOrder = CreateApprovedWorkOrder();
        
        // Mock the new GetByEquipmentNameAsync method
        repository.GetByEquipmentNameAsync(EquipmentName, Arg.Any<CancellationToken>())
            .Returns(new List<WorkOrder> { workOrder });
            
        var executor = new QueryFaultHistoryExecutor(repository, logger);

        var result = await executor.ExecuteAsync(CreateRequest(EquipmentName, "overheating"));

        Assert.True(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.Success, result.Status);
        var response = JsonSerializer.Deserialize<QueryFaultHistoryResponse>(result.ResultJson!);
        var record = Assert.Single(response!.Records);
        Assert.Equal(workOrder.Id, record.EventId);
        Assert.Equal(workOrder.CreatedAtUtc.UtcDateTime, record.OccurredAt);
        Assert.Equal(workOrder.Symptom, record.FaultDescription);
        Assert.Equal(workOrder.DecisionComment, record.Resolution);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsEmpty_LogsWarningAndReturnsEmptyRecords()
    {
        var repository = Substitute.For<IWorkOrderRepository>();
        var logger = Substitute.For<ILogger<QueryFaultHistoryExecutor>>();
        
        repository.GetByEquipmentNameAsync("Unknown-Equipment", Arg.Any<CancellationToken>())
            .Returns(new List<WorkOrder>());
            
        var executor = new QueryFaultHistoryExecutor(repository, logger);

        var result = await executor.ExecuteAsync(CreateRequest("Unknown-Equipment", null));

        Assert.True(result.Succeeded);
        var response = JsonSerializer.Deserialize<QueryFaultHistoryResponse>(result.ResultJson!);
        Assert.Empty(response!.Records);
        Assert.Equal(1, WarningLogCount(logger));
    }

    [Fact]
    public async Task ExecuteAsync_WhenSymptomDoesNotMatch_LogsWarningAndReturnsEmptyRecords()
    {
        var repository = Substitute.For<IWorkOrderRepository>();
        var logger = Substitute.For<ILogger<QueryFaultHistoryExecutor>>();
        var workOrder = CreateApprovedWorkOrder();
        
        repository.GetByEquipmentNameAsync(EquipmentName, Arg.Any<CancellationToken>())
            .Returns(new List<WorkOrder> { workOrder });
            
        var executor = new QueryFaultHistoryExecutor(repository, logger);

        var result = await executor.ExecuteAsync(CreateRequest(EquipmentName, "pressure loss"));

        Assert.True(result.Succeeded);
        var response = JsonSerializer.Deserialize<QueryFaultHistoryResponse>(result.ResultJson!);
        Assert.Empty(response!.Records);
        Assert.Equal(1, WarningLogCount(logger));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ReturnsExecutorFailedAndLogsWarning()
    {
        var repository = Substitute.For<IWorkOrderRepository>();
        var logger = Substitute.For<ILogger<QueryFaultHistoryExecutor>>();
        var exception = new InvalidOperationException("Repository unavailable.");
        
        repository.GetByEquipmentNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<WorkOrder>>(exception));
            
        var executor = new QueryFaultHistoryExecutor(repository, logger);

        var result = await executor.ExecuteAsync(CreateRequest("Some-Equipment", null));

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("QUERY_FAULT_HISTORY_FAILED", result.ErrorCode);
        Assert.Equal(exception.Message, result.ErrorMessage);
        Assert.Equal(1, WarningLogCount(logger));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArgumentsAreInvalidJson_ReturnsExecutorFailedWithJsonExceptionMessage()
    {
        var logger = Substitute.For<ILogger<QueryFaultHistoryExecutor>>();
        var executor = new QueryFaultHistoryExecutor(
            Substitute.For<IWorkOrderRepository>(),
            logger);
        var request = new ToolInvocationRequest(
            "QueryFaultHistory",
            "{ invalid json",
            CreateContext());

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("QUERY_FAULT_HISTORY_FAILED", result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkOrder CreateApprovedWorkOrder()
    {
        var workOrder = new WorkOrder(
            "Overheating investigation",
            "Motor overheating",
            EquipmentName, // Use the constant equipment name
            "technician-1");
        var prerequisite = workOrder.AddSafetyPrerequisite("Isolate equipment");
        workOrder.CompleteSafetyPrerequisite(prerequisite.Id, "technician-1");
        workOrder.SubmitForApproval("technician-1");
        workOrder.Approve("supervisor-1", "Replaced cooling fan.");
        return workOrder;
    }

    // Changed parameter from Guid to string
    private static ToolInvocationRequest CreateRequest(string equipmentIdentifier, string? symptom) =>
        new(
            "QueryFaultHistory",
            JsonSerializer.Serialize(new QueryFaultHistoryRequest(equipmentIdentifier, symptom)),
            CreateContext());

    private static ToolInvocationContext CreateContext() => new(
        Guid.NewGuid(),
        "Technician",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "SymptomMatcher",
        Guid.NewGuid().ToString());

    private static int WarningLogCount(ILogger<QueryFaultHistoryExecutor> logger) =>
        logger.ReceivedCalls().Count(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            call.GetArguments()[0] is LogLevel.Warning);
}
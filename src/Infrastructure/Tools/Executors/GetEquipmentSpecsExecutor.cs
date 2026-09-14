using System.Text.Json;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes equipment specification requests.
/// </summary>
public sealed class GetEquipmentSpecsExecutor : IToolExecutor
{
    private readonly ILogger<GetEquipmentSpecsExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetEquipmentSpecsExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger used to record equipment specification requests.</param>
    public GetEquipmentSpecsExecutor(ILogger<GetEquipmentSpecsExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public string ToolName => "GetEquipmentSpecs";

    /// <inheritdoc />
    public Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var specsRequest = JsonSerializer.Deserialize<GetEquipmentSpecsRequest>(request.ArgumentsJson, options)
            ?? throw new JsonException("Get equipment specs request could not be deserialized.");

        _logger.LogInformation(
            "Getting equipment specifications for equipment {EquipmentId}.",
            specsRequest.EquipmentId);

        var response = new GetEquipmentSpecsResponse(
            "Dummy CNC Machine",
            "X-2000",
            "Acme Corp",
            "Max RPM 5000, Max Temp 80C");
        var resultJson = JsonSerializer.Serialize(response);

        return Task.FromResult(
            new ToolDispatchResult(
                request.ToolName,
                true,
                ToolDispatchStatus.Success,
                resultJson,
                null,
                null));
    }
}

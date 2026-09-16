using System.Text.Json;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes equipment specification requests.
/// </summary>
public sealed class GetEquipmentSpecsExecutor : IToolExecutor
{
    private readonly IEquipmentRepository _repository;
    private readonly ILogger<GetEquipmentSpecsExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetEquipmentSpecsExecutor"/> class.
    /// </summary>
    /// <param name="repository">The repository used to retrieve equipment data.</param>
    /// <param name="logger">The logger used to record equipment specification requests.</param>
    public GetEquipmentSpecsExecutor(
        IEquipmentRepository repository,
        ILogger<GetEquipmentSpecsExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ToolName => "GetEquipmentSpecs";

    /// <inheritdoc />
    public async Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var specsRequest = JsonSerializer.Deserialize<GetEquipmentSpecsRequest>(request.ArgumentsJson, options)
                ?? throw new JsonException("Get equipment specs request could not be deserialized.");

            _logger.LogInformation(
                "Getting equipment specifications for equipment {EquipmentId}.",
                specsRequest.EquipmentId);

            var equipment = await _repository.GetByIdAsync(specsRequest.EquipmentId, cancellationToken);
            if (equipment is null)
            {
                _logger.LogWarning(
                    "Equipment {EquipmentId} was not found.",
                    specsRequest.EquipmentId);
            }

            var response = equipment is null
                ? new GetEquipmentSpecsResponse("Equipment not found", string.Empty, string.Empty, string.Empty)
                : new GetEquipmentSpecsResponse(
                    equipment.Name,
                    equipment.SerialNumber ?? string.Empty,
                    string.Empty,
                    string.Empty);

            return new ToolDispatchResult(
                request.ToolName,
                true,
                ToolDispatchStatus.Success,
                JsonSerializer.Serialize(response),
                null,
                null);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Equipment specification arguments could not be deserialized.");
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "GET_EQUIPMENT_SPECS_FAILED",
                exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Equipment specification lookup failed for tool request {ToolName}.",
                request.ToolName);
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "GET_EQUIPMENT_SPECS_FAILED",
                exception.Message);
        }
    }
}

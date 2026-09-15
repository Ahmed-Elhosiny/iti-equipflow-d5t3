using System.Text.Json;
using System.Text.Json.Nodes;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Dispatcher;

/// <summary>
/// Decorates tool dispatch with schema validation for TL-006, mitigating Insecure Output Handling.
/// </summary>
public sealed class ValidatingToolDispatcher : IToolDispatcher
{
    private static readonly Dictionary<string, string> ToolSchemasMap;
    private readonly IToolDispatcher _innerDispatcher;
    private readonly ILogger<ValidatingToolDispatcher> _logger;

    static ValidatingToolDispatcher()
    {
        ToolSchemasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SearchManuals"] = ToolSchemas.SearchManualsSchema,
            ["QueryFaultHistory"] = ToolSchemas.QueryFaultHistorySchema,
            ["GetEquipmentSpecs"] = ToolSchemas.GetEquipmentSpecsSchema,
            ["GenerateSafetyChecklist"] = ToolSchemas.GenerateSafetyChecklistSchema,
            ["CreateWorkOrder"] = WorkflowToolSchemas.CreateWorkOrderSchema,
            ["ValidateBudget"] = WorkflowToolSchemas.ValidateBudgetSchema,
            ["CheckApprovalStatus"] = WorkflowToolSchemas.CheckApprovalStatusSchema,
            ["EmitAgentEvent"] = WorkflowToolSchemas.EmitAgentEventSchema
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatingToolDispatcher"/> class.
    /// </summary>
    /// <param name="innerDispatcher">The dispatcher to invoke after validation succeeds.</param>
    /// <param name="logger">The logger used to record validation failures.</param>
    public ValidatingToolDispatcher(
        IToolDispatcher innerDispatcher,
        ILogger<ValidatingToolDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(innerDispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _innerDispatcher = innerDispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ToolDispatchResult> DispatchAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (ToolSchemasMap.TryGetValue(request.ToolName, out var schemaText))
        {
            try
            {
                var schema = JsonSchema.FromText(schemaText);
                var jsonNode = JsonNode.Parse(request.ArgumentsJson);
                var jsonElement = jsonNode?.Deserialize<JsonElement>()
                    ?? throw new JsonException("Tool arguments cannot be null.");
                var evaluation = schema.Evaluate(
                    jsonElement,
                    new EvaluationOptions { OutputFormat = OutputFormat.List });

                if (!evaluation.IsValid)
                {
                    const string errorCode = "INVALID_TOOL_ARGUMENTS";
                    var errorMessage = $"Arguments for tool '{request.ToolName}' failed JSON schema validation.";

                    _logger.LogWarning(
                        "Tool invocation blocked by validation. ToolName: {ToolName}, ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                        request.ToolName,
                        errorCode,
                        errorMessage);

                    return new ToolDispatchResult(
                        request.ToolName,
                        false,
                        ToolDispatchStatus.BlockedByValidation,
                        null,
                        errorCode,
                        errorMessage);
                }
            }
            catch (JsonException exception)
            {
                const string errorCode = "INVALID_TOOL_ARGUMENTS";
                var errorMessage = $"Arguments for tool '{request.ToolName}' are not valid JSON.";

                _logger.LogWarning(
                    exception,
                    "Tool invocation blocked because its arguments are not valid JSON. ToolName: {ToolName}, ErrorCode: {ErrorCode}",
                    request.ToolName,
                    errorCode);

                return new ToolDispatchResult(
                    request.ToolName,
                    false,
                    ToolDispatchStatus.BlockedByValidation,
                    null,
                    errorCode,
                    errorMessage);
            }
        }

        return await _innerDispatcher.DispatchAsync(request, cancellationToken);
    }
}
using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;

namespace EquipFlow.Application.Agentic.Agents;

public sealed class WorkOrderGeneratorAgent(
    ILLMProvider llmProvider,
    IToolDispatcher toolDispatcher) : IAgent<WorkOrderInput, WorkOrderOutput>
{
    private static readonly IReadOnlyList<ToolDefinition> AllowedTools =
    [
        new(
            "ValidateBudget",
            "Validate the estimated work order cost against the user's available budget.",
            WorkflowToolSchemas.ValidateBudgetSchema),
        new(
            "CreateWorkOrder",
            "Create the approved work order as a draft in the maintenance system.",
            WorkflowToolSchemas.CreateWorkOrderSchema)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string SystemPrompt = """
        You are an Industrial Maintenance Work Order Drafter.
        Draft a precise work order from the diagnostic plan and safety prerequisites supplied by the user.
        The work order must include a clear title, actionable description, required parts, priority, and estimated cost.
        Include every mandatory safety prerequisite in the description. Do not invent diagnostic evidence or safety controls.
        You may use only ValidateBudget and CreateWorkOrder. Validate the budget before creating the work order.
        Return strictly valid JSON only when asked for the final work order summary.
        The JSON must match this contract exactly:
        {
          "Title": "string",
          "Description": "string",
          "RequiredParts": ["string"],
          "Priority": "string",
          "WorkOrderId": "uuid or null",
          "Summary": "string",
          "EstimatedCost": 0
        }
        """;

    public string Name => "WorkOrderGenerator";

    public async Task<AgentResult<WorkOrderOutput>> ExecuteAsync(
        WorkOrderInput input,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var userPrompt = $"""
            Draft a work order for equipment {input.EquipmentId}.

            Diagnostic plan:
            {JsonSerializer.Serialize(input.DiagnosticPlan, JsonOptions)}

            Use the diagnostic steps and safety prerequisites above to determine the title, description,
            required parts, priority, and estimated cost. Request ValidateBudget before CreateWorkOrder.
            """;

        var completion = await llmProvider.CompleteAsync(
            new CompletionRequest(userPrompt, SystemPrompt, Tools: AllowedTools),
            cancellationToken);

        if (completion.ToolCalls is null || completion.ToolCalls.Count == 0)
        {
            return Failure<WorkOrderOutput>(
                "The work order generator did not request budget validation and work order creation.");
        }

        foreach (var toolCall in completion.ToolCalls)
        {
            if (!AllowedTools.Any(tool => string.Equals(tool.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Failure<WorkOrderOutput>(
                    $"Tool '{toolCall.Name}' is not allowed for agent '{Name}'.");
            }
        }

        var budgetCalls = completion.ToolCalls
            .Where(toolCall => string.Equals(toolCall.Name, "ValidateBudget", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (budgetCalls.Length == 0)
        {
            return Failure<WorkOrderOutput>("The work order generator must validate the budget before creation.");
        }

        ToolDispatchResult? approvedBudget = null;
        foreach (var toolCall in budgetCalls)
        {
            approvedBudget = await DispatchAsync(toolCall, context, cancellationToken);
            if (!approvedBudget.Succeeded)
            {
                return Failure<WorkOrderOutput>(
                    approvedBudget.ErrorMessage ?? "Budget validation failed.");
            }

            if (!IsBudgetApproved(approvedBudget.ResultJson, out var budgetError))
            {
                return Failure<WorkOrderOutput>(budgetError);
            }
        }

        var createCall = completion.ToolCalls.FirstOrDefault(
            toolCall => string.Equals(toolCall.Name, "CreateWorkOrder", StringComparison.OrdinalIgnoreCase));
        if (createCall is null)
        {
            return Failure<WorkOrderOutput>(
                "The budget was approved, but the work order generator did not request creation.");
        }

        var created = await DispatchAsync(createCall, context, cancellationToken);
        if (!created.Succeeded)
        {
            return Failure<WorkOrderOutput>(
                created.ErrorMessage ?? "Work order creation failed.");
        }

        var createdWorkOrderId = ReadWorkOrderId(created.ResultJson);
        var finalCompletion = await llmProvider.CompleteAsync(
            new CompletionRequest(
                $"{userPrompt}\n\nBudget validation result:\n{approvedBudget!.ResultJson}\n\nCreateWorkOrder result:\n{created.ResultJson}\n\nReturn the final work order summary now.",
                SystemPrompt),
            cancellationToken);

        try
        {
            var output = JsonSerializer.Deserialize<WorkOrderOutput>(finalCompletion.Text, JsonOptions)
                ?? throw new JsonException("The work order generator returned an empty result.");

            return new AgentResult<WorkOrderOutput>(
                output with
                {
                    WorkOrderId = createdWorkOrderId ?? output.WorkOrderId,
                    Summary = string.IsNullOrWhiteSpace(output.Summary)
                        ? output.Description
                        : output.Summary,
                    EstimatedCost = output.EstimatedCost == 0
                        ? ReadEstimatedCost(createCall.ArgumentsJson)
                        : output.EstimatedCost,
                    RequiredParts = output.RequiredParts ?? []
                },
                []);
        }
        catch (JsonException exception)
        {
            return Failure<WorkOrderOutput>(
                $"The work order generator returned invalid structured output: {exception.Message}");
        }
    }

    private async Task<ToolDispatchResult> DispatchAsync(
        ToolCall toolCall,
        IAgentContext context,
        CancellationToken cancellationToken) =>
        await toolDispatcher.DispatchAsync(
            new ToolInvocationRequest(
                toolCall.Name,
                toolCall.ArgumentsJson,
                CreateInvocationContext(context)),
            cancellationToken);

    private ToolInvocationContext CreateInvocationContext(IAgentContext context) =>
        new(
            ParseGuid(context.UserId),
            "Technician",
            ParseGuid(context.CorrelationId),
            Guid.NewGuid(),
            Name,
            context.CorrelationId);

    private static bool IsBudgetApproved(string? resultJson, out string error)
    {
        error = "Budget validation was rejected.";
        try
        {
            using var document = JsonDocument.Parse(resultJson ?? throw new JsonException("Budget result was empty."));
            if (!document.RootElement.TryGetProperty("IsApproved", out var isApproved) || !isApproved.GetBoolean())
            {
                error = document.RootElement.TryGetProperty("RejectionReason", out var reason)
                    ? reason.GetString() ?? error
                    : error;
                return false;
            }

            return true;
        }
        catch (JsonException exception)
        {
            error = $"Budget validation returned invalid structured output: {exception.Message}";
            return false;
        }
    }

    private static Guid? ReadWorkOrderId(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(resultJson);
        return document.RootElement.TryGetProperty("WorkOrderId", out var id)
            && id.TryGetGuid(out var workOrderId)
            ? workOrderId
            : null;
    }

    private static decimal ReadEstimatedCost(string argumentsJson)
    {
        using var document = JsonDocument.Parse(argumentsJson);
        return document.RootElement.TryGetProperty("estimatedCost", out var cost)
            ? cost.GetDecimal()
            : 0;
    }

    private static Guid ParseGuid(string value) =>
        Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;

    private static AgentResult<T> Failure<T>(string error) =>
        new(default!, [], false, error);
}
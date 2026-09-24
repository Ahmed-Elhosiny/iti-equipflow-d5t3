using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Events;
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
            "DraftWorkOrder",
            "Draft the approved work order details for the maintenance system. This does not save to the database.",
            WorkflowToolSchemas.DraftWorkOrderSchema)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string SystemPrompt = """
        You are an Industrial Maintenance Work Order Drafter.
        Draft a precise work order from the diagnostic plan and safety prerequisites supplied by the user.
        The work order must include a clear title, actionable description, required parts, priority, and estimated cost.
        Include every mandatory safety prerequisite in the description. Do not invent diagnostic evidence or safety controls.
        You may use only ValidateBudget and DraftWorkOrder. Validate the budget before drafting the work order.
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
            required parts, priority, and estimated cost. Request ValidateBudget before DraftWorkOrder.
            """;

        var completion = await AgentEventRecorder.CompleteAsync(
            llmProvider,
            new CompletionRequest(userPrompt, SystemPrompt, Tools: AllowedTools),
            context,
            Name,
            1,
            cancellationToken);

        if (completion.ToolCalls is null || completion.ToolCalls.Count == 0)
        {
            return Failure<WorkOrderOutput>(
                "The work order generator did not request budget validation and work order drafting.");
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

        var draftCall = completion.ToolCalls.FirstOrDefault(
            toolCall => string.Equals(toolCall.Name, "DraftWorkOrder", StringComparison.OrdinalIgnoreCase));
        if (draftCall is null)
        {
            return Failure<WorkOrderOutput>(
                "The budget was approved, but the work order generator did not request drafting.");
        }

        var drafted = await DispatchAsync(draftCall, context, cancellationToken);
        if (!drafted.Succeeded)
        {
            return Failure<WorkOrderOutput>(
                drafted.ErrorMessage ?? "Work order drafting failed.");
        }

        var finalCompletion = await AgentEventRecorder.CompleteAsync(
            llmProvider,
            new CompletionRequest(
                $"{userPrompt}\n\nBudget validation result:\n{approvedBudget!.ResultJson}\n\nDraftWorkOrder result:\n{drafted.ResultJson}\n\nReturn the final work order summary now.",
                SystemPrompt),
            context,
            Name,
            2,
            cancellationToken);

        try
        {
            var output = JsonSerializer.Deserialize<WorkOrderOutput>(finalCompletion.Text, JsonOptions)
                ?? throw new JsonException("The work order generator returned an empty result.");

            return new AgentResult<WorkOrderOutput>(
                output with
                {
                    WorkOrderId = null, // Draft only, DB persistence happens at the API/Orchestrator layer
                    Summary = string.IsNullOrWhiteSpace(output.Summary)
                        ? output.Description
                        : output.Summary,
                    EstimatedCost = output.EstimatedCost == 0
                        ? ReadEstimatedCost(draftCall.ArgumentsJson)
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
        await AgentEventRecorder.DispatchAsync(
            toolDispatcher,
            new ToolInvocationRequest(
                toolCall.Name,
                toolCall.ArgumentsJson,
                CreateInvocationContext(context)),
            context,
            Name,
            1,
            toolCall.Id,
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
                error = document.RootElement.TryGetProperty("Reason", out var reason)
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
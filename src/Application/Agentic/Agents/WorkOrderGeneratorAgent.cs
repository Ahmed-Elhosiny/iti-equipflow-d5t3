using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Application.Options;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Options;

namespace EquipFlow.Application.Agentic.Agents;

public sealed class WorkOrderGeneratorAgent(
    ILLMProvider llmProvider,
    IToolDispatcher toolDispatcher,
    IOptions<AgenticOptions> options) : IAgent<WorkOrderInput, WorkOrderOutput>
{
    private readonly int _maxIterations = options.Value.MaxIterations;

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

        var baseUserPrompt = $"""
            Draft a work order for equipment {input.EquipmentId}.

            Diagnostic plan:
            {JsonSerializer.Serialize(input.DiagnosticPlan, JsonOptions)}

            Use the diagnostic steps and safety prerequisites above to determine the title, description,
            required parts, priority, and estimated cost. Request ValidateBudget before DraftWorkOrder.
            """;

        int iteration = 0;
        string currentPrompt = baseUserPrompt;
        string currentSystemPrompt = SystemPrompt;
        string? finalText = null;
        
        ToolDispatchResult? approvedBudget = null;
        ToolCall? draftCall = null;

        // --- BOUNDED ITERATION LOOP (FR-024 / AG-008) ---
        while (iteration < _maxIterations)
        {
            iteration++;
            var completion = await AgentEventRecorder.CompleteAsync(
                llmProvider,
                new CompletionRequest(currentPrompt, currentSystemPrompt, Tools: AllowedTools),
                context,
                Name,
                iteration,
                cancellationToken);

            if (completion.ToolCalls is null || completion.ToolCalls.Count == 0)
            {
                finalText = completion.Text;
                break;
            }

            foreach (var toolCall in completion.ToolCalls)
            {
                if (!AllowedTools.Any(tool => string.Equals(tool.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return Failure<WorkOrderOutput>($"Tool '{toolCall.Name}' is not allowed for agent '{Name}'.");
                }

                var dispatchResult = await DispatchAsync(toolCall, context, cancellationToken);
                if (!dispatchResult.Succeeded)
                {
                    return Failure<WorkOrderOutput>(dispatchResult.ErrorMessage ?? $"Tool '{toolCall.Name}' failed.");
                }

                if (string.Equals(toolCall.Name, "ValidateBudget", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsBudgetApproved(dispatchResult.ResultJson, out var budgetError))
                    {
                        return Failure<WorkOrderOutput>(budgetError);
                    }
                    approvedBudget = dispatchResult;
                }
                else if (string.Equals(toolCall.Name, "DraftWorkOrder", StringComparison.OrdinalIgnoreCase))
                {
                    draftCall = toolCall;
                }

                currentPrompt += $"\n\nTool '{toolCall.Name}' result:\n{dispatchResult.ResultJson}";
            }
            
            currentSystemPrompt = SystemPrompt + "\nYou have executed tools. You may use more tools if needed, or return the final JSON work order summary if you have completed drafting.";
        }

        if (finalText is null)
        {
            // Iteration Breaker Triggered
            return Failure<WorkOrderOutput>($"Agent '{Name}' exceeded maximum tool-calling iterations ({_maxIterations}) without producing a final output.");
        }
        
        if (approvedBudget is null)
        {
            return Failure<WorkOrderOutput>("The work order generator must validate the budget before creation.");
        }

        // --- JSON SCHEMA RETRY LOOP ---
        const int MaxRetries = 2;
        string? lastError = null;
        WorkOrderOutput? output = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            string textToParse;
            
            if (attempt == 0)
            {
                textToParse = finalText;
            }
            else
            {
                string promptForAttempt = $"{currentPrompt}\n\nYour previous response was invalid JSON and failed to parse with the following error:\n{lastError}\nPlease correct your response and return strictly valid JSON matching the schema. Do not include Markdown or commentary.";

                var finalCompletion = await AgentEventRecorder.CompleteAsync(
                    llmProvider,
                    new CompletionRequest(promptForAttempt, SystemPrompt),
                    context,
                    Name,
                    iteration + attempt,
                    cancellationToken);
                    
                textToParse = finalCompletion.Text;
            }

            try
            {
                output = JsonSerializer.Deserialize<WorkOrderOutput>(textToParse, JsonOptions)
                    ?? throw new JsonException("The work order generator returned an empty result.");
                break; 
            }
            catch (JsonException exception)
            {
                lastError = exception.Message;
                if (attempt == MaxRetries)
                {
                    return Failure<WorkOrderOutput>(
                        $"The work order generator returned invalid structured output after {MaxRetries + 1} attempts: {lastError}");
                }
            }
        }

        return new AgentResult<WorkOrderOutput>(
            output! with
            {
                WorkOrderId = null, 
                Summary = string.IsNullOrWhiteSpace(output!.Summary)
                    ? output.Description
                    : output.Summary,
                EstimatedCost = output.EstimatedCost == 0 && draftCall is not null
                    ? ReadEstimatedCost(draftCall.ArgumentsJson)
                    : output.EstimatedCost,
                RequiredParts = output.RequiredParts ?? []
            },
            []);
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
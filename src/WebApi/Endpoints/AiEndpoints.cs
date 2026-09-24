using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Commands;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Application.Agentic.Orchestration;
using EquipFlow.Application.Agentic.Queries;
using EquipFlow.WebApi.Middleware;
using MediatR;

namespace EquipFlow.WebApi.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", Chat)
            .WithName("Chat")
            .WithTags("Chat")
            .WithSummary("Conversational AI entry point with SSE streaming")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/ai/analyze", AnalyzeMaintenance)
            .WithName("AnalyzeMaintenance")
            .WithTags("AI")
            .RequireAuthorization(policy => policy.RequireRole(
                "Technician",
                "Engineer",
                "Manager"))
            .Produces<AnalyzeMaintenanceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/api/runs/{runId:guid}", GetAgentRunById)
            .WithName("GetAgentRunById")
            .WithTags("Observability")
            .WithSummary("Inspect an agent run")
            .WithDescription("Returns the chronological events and outcome for an agent run.")
            .RequireAuthorization()
            .Produces<AgentRunDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/runs/{runId:guid}/cancel", CancelAgentRun)
            .WithName("CancelAgentRun")
            .WithTags("Observability")
            .WithSummary("Cancel an active agent run")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> Chat(
        HttpContext httpContext,
        ChatRequest request,
        SequentialSupervisorOrchestrator orchestrator,
        IActiveRunRegistry runRegistry,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString() ?? Guid.NewGuid().ToString();
        var runId = Guid.TryParse(correlationId, out var parsedRunId) ? parsedRunId : Guid.NewGuid();
        
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runRegistry.Register(runId, runCts);

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("X-Correlation-Id", correlationId);
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");

        var channel = Channel.CreateUnbounded<AgentEventBase>();

        try
        {
            var maintenanceRequest = new MaintenanceRequest(
                userId,
                request.Message,
                request.EquipmentContext);
            var agentContext = new AgentContext(correlationId, userId);

            void OnAgentEvent(AgentEventBase evt) => channel.Writer.TryWrite(evt);

            var orchestratorTask = Task.Run(async () =>
            {
                try
                {
                    return await orchestrator.RunWorkflowAsync(
                        maintenanceRequest,
                        agentContext,
                        OnAgentEvent,
                        runCts.Token);
                }
                finally
                {
                    channel.Writer.TryComplete();
                }
            }, runCts.Token);

            async Task EmitEventAsync(ChatStreamEvent streamEvent)
            {
                var json = JsonSerializer.Serialize(streamEvent, 
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await httpContext.Response.WriteAsync($"event: {streamEvent.EventType}\ndata: {json}\n\n", runCts.Token);
                await httpContext.Response.Body.FlushAsync(runCts.Token);
            }

            await foreach (var evt in channel.Reader.ReadAllAsync(runCts.Token))
            {
                var eventType = evt.GetType().Name switch
                {
                    nameof(AgentRunStarted) => "agent.started",
                    nameof(AgentRunCompleted) => "agent.completed",
                    _ => "agent.progress"
                };
                await EmitEventAsync(new ChatStreamEvent(eventType, runId, null, evt));
            }

            var workflowResult = await orchestratorTask;

            if (workflowResult.Status == WorkflowStatus.Blocked)
            {
                await EmitEventAsync(new ChatStreamEvent("budget.exhausted", runId, null, new 
                { 
                    status = "budget_exhausted", 
                    remaining = workflowResult.RemainingBudget?.ToString("C") ?? "$0.00",
                    estimatedCost = workflowResult.EstimatedCost?.ToString("C") ?? "$0.00",
                    options = new[] { "wait_for_reset" }
                }));
            }
            else if (workflowResult.Status == WorkflowStatus.Failed || workflowResult.Status == WorkflowStatus.PartialSuccess)
            {
                await EmitEventAsync(new ChatStreamEvent("refusal", runId, null, new { error = workflowResult.ErrorMessage }));
            }
            else
            {
                var finalText = workflowResult.CachedResponse ?? workflowResult.Draft?.Summary ?? "Workflow completed.";
                
                // Simulate token-level streaming by chunking the final text
                var words = finalText.Split(' ');
                foreach (var word in words)
                {
                    await EmitEventAsync(new ChatStreamEvent("token", runId, null, new { content = word + " " }));
                    await Task.Delay(20, runCts.Token);
                }

                if (workflowResult.Status == WorkflowStatus.PendingApproval && workflowResult.Draft is not null)
                {
                    await EmitEventAsync(new ChatStreamEvent("citation", runId, null, null, new[] { new CitationDto("draft-generated", "system", "Draft Work Order", 1, 1.0f) }));
                }
            }

            await EmitEventAsync(new ChatStreamEvent("done", runId, null, new { status = workflowResult.Status.ToString() }));
            
            return Results.Empty;
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        finally
        {
            runRegistry.Unregister(runId);
        }
    }

    private static async Task<IResult> CancelAgentRun(
        Guid runId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var cancelled = await sender.Send(new CancelAgentRunCommand(runId), cancellationToken);
        return cancelled ? Results.NoContent() : Results.NotFound();
    }

       private static async Task<IResult> GetAgentRunById(
        Guid runId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new GetAgentRunByIdQuery(runId, userId.Value.ToString()),
            cancellationToken);

        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> AnalyzeMaintenance(
        HttpContext httpContext,
        AnalyzeMaintenanceRequest request,
        SequentialSupervisorOrchestrator orchestrator,
        IActiveRunRegistry runRegistry,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Correlation ID Missing");
        }

        var runId = Guid.TryParse(correlationId, out var parsedRunId) ? parsedRunId : Guid.NewGuid();
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        runRegistry.Register(runId, runCts);

        try
        {
            var maintenanceRequest = new MaintenanceRequest(
                userId,
                request.SymptomDescription,
                request.EquipmentIdHint);
            var agentContext = new AgentContext(correlationId, userId);

            var workflowResult = await orchestrator.RunWorkflowAsync(
                maintenanceRequest,
                agentContext,
                null,
                runCts.Token);

            return workflowResult.Status switch
            {
                WorkflowStatus.PendingApproval
                    or WorkflowStatus.PartialSuccess
                    or WorkflowStatus.Cached => Results.Ok(ToResponse(workflowResult)),
                WorkflowStatus.Blocked => Results.Problem(
                    statusCode: StatusCodes.Status402PaymentRequired,
                    title: "Budget Exhausted",
                    detail: workflowResult.ErrorMessage),
                WorkflowStatus.Failed => Results.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Workflow Failed",
                    detail: workflowResult.ErrorMessage),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unknown Workflow Status")
            };
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        finally
        {
            runRegistry.Unregister(runId);
        }
    }

       private static AnalyzeMaintenanceResponse ToResponse(WorkflowResult result) =>
        new(
            result.Status,
            result.Draft,
            result.DiagnosticPlan,
            result.ErrorMessage,
            result.ReasonCode,
            result.EstimatedCost,
            result.RemainingBudget,
            result.CachedResponse,
            result.FallbackSearchResults?.Select(r => new CitationDto(r.DocumentId.ToString(), r.ChunkId.ToString(), null, null, (float)r.Score)));


    private sealed record AgentContext(string CorrelationId, string UserId) : IAgentContext;
}

public record ChatRequest(
    string Message,
    Guid? ConversationId = null,
    string? EquipmentContext = null,
    bool Stream = true);

public record ChatStreamEvent(
    string EventType,
    Guid RunId,
    string? AgentId = null,
    object? Data = null,
    IEnumerable<CitationDto>? Citations = null);

public record CitationDto(
    string DocumentId,
    string ChunkId,
    string? Section = null,
    int? Page = null,
    float Score = 1.0f);

public record AnalyzeMaintenanceRequest(
    string SymptomDescription,
    string? EquipmentIdHint = null);

public record AnalyzeMaintenanceResponse(
    WorkflowStatus Status,
    WorkOrderOutput? Draft = null,
    DiagnosticPlanOutput? DiagnosticPlan = null,
    string? ErrorMessage = null,
    string? ReasonCode = null,
    decimal? EstimatedCost = null,
    decimal? RemainingBudget = null,
    string? CachedResponse = null,
    IEnumerable<CitationDto>? FallbackCitations = null);
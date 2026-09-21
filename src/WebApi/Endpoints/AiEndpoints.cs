using System.Security.Claims;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Commands;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Orchestration;
using EquipFlow.Application.Agentic.Queries;
using EquipFlow.WebApi.Middleware;
using MediatR;

namespace EquipFlow.WebApi.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
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
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAgentRunByIdQuery(runId),
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
        
        // Register the run so it can be cancelled externally
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
            // Return 499 Client Closed Request if the run was cancelled
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
            result.CachedResponse);

    private sealed record AgentContext(string CorrelationId, string UserId) : IAgentContext;
}

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
    string? CachedResponse = null);
using System.Security.Claims;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Orchestration;
using EquipFlow.WebApi.Middleware;

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
            .Produces<WorkflowResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> AnalyzeMaintenance(
        HttpContext httpContext,
        AnalyzeMaintenanceRequest request,
        SequentialSupervisorOrchestrator orchestrator,
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

        var maintenanceRequest = new MaintenanceRequest(
            userId,
            request.SymptomDescription,
            request.EquipmentIdHint);
        var agentContext = new AgentContext(correlationId, userId);

        var workflowResult = await orchestrator.RunWorkflowAsync(
            maintenanceRequest,
            agentContext,
            cancellationToken);

        return workflowResult.Status switch
        {
            WorkflowStatus.PendingApproval
                or WorkflowStatus.PartialSuccess
                or WorkflowStatus.Cached => Results.Ok(workflowResult),
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

    private sealed record AgentContext(string CorrelationId, string UserId) : IAgentContext;
}

public record AnalyzeMaintenanceRequest(
    string SymptomDescription,
    string? EquipmentIdHint = null);
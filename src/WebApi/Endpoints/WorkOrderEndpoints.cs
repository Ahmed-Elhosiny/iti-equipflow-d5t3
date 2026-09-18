using System.Security.Claims;
using EquipFlow.Application.Common;
using EquipFlow.Application.WorkOrders.Commands;
using EquipFlow.Application.WorkOrders.Queries;
using EquipFlow.Domain.Enums;
using EquipFlow.WebApi.Middleware;
using MediatR;

namespace EquipFlow.WebApi.Endpoints;

public static class WorkOrderEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workorders/{id:guid}", GetById)
            .WithName("GetWorkOrderById")
            .WithSummary("Get a work order by ID")
            .WithTags("Work Orders")
            .RequireAuthorization()
            .Produces<WorkOrderDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/workorders", GetByUser)
            .WithName("GetWorkOrdersByUser")
            .WithSummary("Get the authenticated user's work orders")
            .WithTags("Work Orders")
            .RequireAuthorization()
            .Produces<IEnumerable<WorkOrderDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/workorders/pending-approvals", GetPendingApprovals)
            .WithName("GetPendingWorkOrderApprovals")
            .WithSummary("Get work orders pending supervisor approval")
            .WithTags("Work Orders")
            .RequireAuthorization("Supervisor")
            .Produces<IEnumerable<WorkOrderDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapPost("/api/workorders/{id:guid}/submit", Submit)
            .WithName("SubmitWorkOrder")
            .WithTags("Work Orders")
            .RequireAuthorization(policy => policy.RequireRole("Technician", "Engineer", "Manager"))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapPost("/api/workorders/{id:guid}/approve", Approve)
            .WithName("ApproveWorkOrder")
            .WithTags("Work Orders")
            .RequireAuthorization("Supervisor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapPost("/api/workorders/{id:guid}/reject", Reject)
            .WithName("RejectWorkOrder")
            .WithTags("Work Orders")
            .RequireAuthorization("Supervisor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapPost("/api/workorders/{id:guid}/dispatch", Dispatch)
            .WithName("DispatchWorkOrder")
            .WithTags("Work Orders")
            .RequireAuthorization("Supervisor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> GetById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var workOrder = await sender.Send(
                new GetWorkOrderByIdQuery(id),
                cancellationToken);
            return Results.Ok(workOrder);
        }
        catch (WorkOrderNotFoundException exception)
        {
            return Results.NotFound(exception.Message);
        }
    }

    private static async Task<IResult> GetByUser(
        ClaimsPrincipal user,
        WorkOrderStatus? status,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var workOrders = await sender.Send(
            new GetWorkOrdersByUserIdQuery(userId.Value, status),
            cancellationToken);
        return Results.Ok(workOrders);
    }

    private static async Task<IResult> GetPendingApprovals(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var workOrders = await sender.Send(
            new GetPendingApprovalsQuery(),
            cancellationToken);
        return Results.Ok(workOrders);
    }

    private static Task<IResult> Submit(
        Guid id,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken) =>
        ExecuteTransition(
            httpContext,
            sender,
            new SubmitWorkOrderForApprovalCommand(
                id,
                GetUserId(httpContext) ?? string.Empty,
                GetCorrelationId(httpContext)),
            cancellationToken);

    private static Task<IResult> Approve(
        Guid id,
        WorkOrderDecisionRequest? request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken) =>
        ExecuteTransition(
            httpContext,
            sender,
            new ReviewWorkOrderCommand(
                id,
                WorkOrderReviewDecision.Approve,
                GetUserId(httpContext) ?? string.Empty,
                request?.Comment,
                GetCorrelationId(httpContext)),
            cancellationToken);

    private static Task<IResult> Reject(
        Guid id,
        WorkOrderDecisionRequest? request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken) =>
        ExecuteTransition(
            httpContext,
            sender,
            new ReviewWorkOrderCommand(
                id,
                WorkOrderReviewDecision.Reject,
                GetUserId(httpContext) ?? string.Empty,
                request?.Comment,
                GetCorrelationId(httpContext)),
            cancellationToken);

    private static Task<IResult> Dispatch(
        Guid id,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken) =>
        ExecuteTransition(
            httpContext,
            sender,
            new DispatchWorkOrderCommand(
                id,
                GetUserId(httpContext) ?? string.Empty,
                GetCorrelationId(httpContext)),
            cancellationToken);

    private static async Task<IResult> ExecuteTransition<TCommand>(
        HttpContext httpContext,
        ISender sender,
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : notnull, IRequest
    {
        if (string.IsNullOrWhiteSpace(GetUserId(httpContext)))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(GetCorrelationId(httpContext)))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Correlation ID Missing");
        }

        try
        {
            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(exception.Message);
        }
        catch (WorkOrderNotFoundException exception)
        {
            return Results.NotFound(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }

    private static string? GetUserId(HttpContext httpContext) =>
        httpContext.User.GetUserId()?.ToString();

    private static string? GetCorrelationId(HttpContext httpContext) =>
        httpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

    private sealed record WorkOrderDecisionRequest(string? Comment = null);
}

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
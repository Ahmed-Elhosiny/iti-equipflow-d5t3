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
        app.MapPost("/api/workorders", Create)
            .WithName("CreateWorkOrder")
            .WithSummary("Create a new draft work order")
            .WithTags("Work Orders")
            .RequireAuthorization(policy => policy.RequireRole("Technician", "Engineer", "Manager"))
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapPost("/api/workorders/{id:guid}/safety", AddSafetyPrerequisite)
            .WithName("AddSafetyPrerequisite")
            .WithSummary("Add a safety prerequisite to a work order")
            .WithTags("Work Orders")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // --- NEW ENDPOINT FOR ISSUE #121 ---
        app.MapPost("/api/workorders/{id:guid}/safety/{pid:guid}/complete", CompleteSafety)
            .WithName("CompleteSafetyPrerequisite")
            .WithSummary("Mark a safety prerequisite as completed")
            .WithTags("Work Orders")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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

                app.MapPost("/api/workorders/{id:guid}/edit-and-approve", EditAndApprove)
            .WithName("EditAndApproveWorkOrder")
            .WithSummary("Edit permitted fields and approve a work order")
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

    // --- NEW HANDLER METHOD FOR ISSUE #121 ---
    private static Task<IResult> CompleteSafety(
        Guid id,
        Guid pid,
        CompleteSafetyPrerequisiteRequest? request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken) =>
        ExecuteTransition(
            httpContext,
            sender,
            new CompleteSafetyPrerequisiteCommand(
                id,
                pid,
                GetUserId(httpContext) ?? string.Empty,
                request?.CompletionNote),
            cancellationToken);

    private static async Task<IResult> AddSafetyPrerequisite(
        Guid id,
        AddSafetyPrerequisiteRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(GetCorrelationId(httpContext)))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Correlation ID Missing");
        }

        var command = new AddSafetyPrerequisiteCommand(
            WorkOrderId: id,
            Description: request.Description,
            IsMandatory: request.IsMandatory,
            SortOrder: request.SortOrder);

        try
        {
            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        }
        catch (WorkOrderNotFoundException exception)
        {
            return Results.NotFound(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(exception.Message);
        }
    }

    private static async Task<IResult> Create(
        CreateWorkOrderRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(GetCorrelationId(httpContext)))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Correlation ID Missing");
        }

        var command = new CreateWorkOrderCommand(
            Title: request.Title,
            Symptom: request.Symptom,
            EquipmentName: request.EquipmentName,
            CreatedBy: userId.Value.ToString(),
            EquipmentAssetNumber: request.EquipmentAssetNumber,
            ManualRevision: request.ManualRevision,
            Location: request.Location);

        try
        {
            var workOrderId = await sender.Send(command, cancellationToken);
            return Results.Created($"/api/workorders/{workOrderId}", workOrderId);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(exception.Message);
        }
    }

      private static async Task<IResult> GetById(
        Guid id,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        try
        {
            var workOrder = await sender.Send(
                new GetWorkOrderByIdQuery(id, userId.Value.ToString()),
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

        private static Task<IResult> EditAndApprove(
        Guid id,
        WorkOrderEditAndApproveRequest? request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken) =>
        ExecuteTransition(
            httpContext,
            sender,
            new ReviewWorkOrderCommand(
                id,
                WorkOrderReviewDecision.EditAndApprove,
                GetUserId(httpContext) ?? string.Empty,
                request?.Comment,
                GetCorrelationId(httpContext),
                request?.Title,
                request?.Symptom,
                request?.EquipmentName,
                request?.EquipmentAssetNumber,
                request?.ManualRevision,
                request?.Location),
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
        private sealed record WorkOrderEditAndApproveRequest(
        string? Comment = null,
        string? Title = null,
        string? Symptom = null,
        string? EquipmentName = null,
        string? EquipmentAssetNumber = null,
        string? ManualRevision = null,
        string? Location = null);
    
    private sealed record CreateWorkOrderRequest(
        string Title,
        string Symptom,
        string EquipmentName,
        string? EquipmentAssetNumber = null,
        string? ManualRevision = null,
        string? Location = null);

    private sealed record AddSafetyPrerequisiteRequest(
        string Description,
        bool IsMandatory,
        int SortOrder);

    // --- NEW REQUEST DTO FOR ISSUE #121 ---
    private sealed record CompleteSafetyPrerequisiteRequest(string? CompletionNote = null);
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
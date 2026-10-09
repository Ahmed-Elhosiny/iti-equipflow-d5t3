using EquipFlow.Application.Budget.Commands;
using EquipFlow.Application.Budget.Queries;
using EquipFlow.Application.CostGovernor.Queries;
using MediatR;

namespace EquipFlow.WebApi.Endpoints;

/// <summary>
/// Maps the Cost Governor budget endpoints.
/// </summary>
public static class CostGovernorEndpoints
{
    /// <summary>
    /// Maps authenticated budget routes.
    /// </summary>
    public static IEndpointRouteBuilder MapCostGovernorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/budget/me", GetMyBudget)
            .WithName("GetMyBudget")
            .WithSummary("Retrieves the current budget status for the authenticated user.")
            .RequireAuthorization();

        endpoints.MapGet("/api/budgets/me", GetMyBudget)
            .WithName("GetMyBudgetLegacy")
            .WithSummary("Get the authenticated user's budget")
            .RequireAuthorization();

        endpoints.MapGet("/api/budgets", GetAllBudgets)
            .WithName("GetAllBudgets")
            .WithSummary("Get all user budgets")
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        endpoints.MapGet("/api/cost/spend", GetMySpend)
            .WithName("GetMySpend")
            .WithSummary("Get the authenticated user's spend history")
            .RequireAuthorization();

        // FIXED: Extracted to static methods to resolve lambda return type inference errors
        endpoints.MapPost("/api/budgets/me/increase-request", RequestBudgetIncrease)
            .WithName("RequestBudgetIncrease")
            .RequireAuthorization();

        endpoints.MapPost("/api/budgets/increase-requests/{id}/review", ReviewBudgetRequest)
            .WithName("ReviewBudgetRequest")
            .RequireAuthorization(policy => policy.RequireRole("Supervisor", "Manager"));

        endpoints.MapGet("/api/budgets/increase-requests/pending", GetPendingBudgetRequests)
            .WithName("GetPendingBudgetRequests")
            .WithSummary("Get all pending budget increase requests (Supervisor/Manager only)")
            .RequireAuthorization(policy => policy.RequireRole("Supervisor", "Manager"));

        return endpoints;
    }

    private static async Task<IResult> GetMyBudget(
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        if (!userId.HasValue) return TypedResults.Unauthorized();

        try
        {
            var budget = await sender.Send(new GetMyBudgetQuery(userId.Value), cancellationToken);
            return budget is null ? TypedResults.NotFound() : TypedResults.Ok(budget);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> GetAllBudgets(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var budgets = await sender.Send(new GetAllBudgetsQuery(), cancellationToken);
        return TypedResults.Ok(budgets);
    }

    private static async Task<IResult> GetMySpend(
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        if (!userId.HasValue) return TypedResults.Unauthorized();

        var spendHistory = await sender.Send(new GetMySpendQuery(userId.Value), cancellationToken);
        return TypedResults.Ok(spendHistory);
    }

    /// <summary>
    /// Creates a new budget increase request for the authenticated user.
    /// </summary>
    private static async Task<IResult> RequestBudgetIncrease(
        HttpContext httpContext,
        RequestBudgetIncreaseRequest req,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        if (!userId.HasValue) return TypedResults.Unauthorized();
        
        var id = await sender.Send(new RequestBudgetIncreaseCommand(userId.Value, req.Amount, req.Reason), cancellationToken);
        return TypedResults.Ok(new { RequestId = id });
    }
    
    /// <summary>
    /// Retrieves all pending budget increase requests for supervisor review.
    /// </summary>
    private static async Task<IResult> GetPendingBudgetRequests(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var requests = await sender.Send(new GetPendingBudgetRequestsQuery(), cancellationToken);
        return TypedResults.Ok(requests);
    }
    /// <summary>
    /// Reviews (approves or rejects) a pending budget increase request.
    /// </summary>
    private static async Task<IResult> ReviewBudgetRequest(
        Guid id,
        HttpContext httpContext,
        ReviewBudgetRequestRequest req,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var reviewerId = httpContext.User.GetUserId();
        if (!reviewerId.HasValue) return TypedResults.Unauthorized();
        
        var success = await sender.Send(new ReviewBudgetRequestCommand(id, reviewerId.Value, req.IsApproved), cancellationToken);
        return success ? TypedResults.Ok() : TypedResults.NotFound();
    }

    private sealed record RequestBudgetIncreaseRequest(decimal Amount, string Reason);
    private sealed record ReviewBudgetRequestRequest(bool IsApproved);
}
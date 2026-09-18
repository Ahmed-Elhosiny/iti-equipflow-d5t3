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
            .WithDescription("Retrieves the current budget status for the authenticated user.")
            .RequireAuthorization();

        endpoints.MapGet("/api/budgets/me", GetMyBudget)
            .WithName("GetMyBudgetLegacy")
            .WithSummary("Get the authenticated user's budget")
            .WithDescription("Returns the budget and recent spend for the authenticated user.")
            .RequireAuthorization();

        endpoints.MapGet("/api/budgets", GetAllBudgets)
            .WithName("GetAllBudgets")
            .WithSummary("Get all user budgets")
            .WithDescription("Returns all user budgets. This operation is restricted to managers.")
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        return endpoints;
    }

    /// <summary>
    /// Gets the budget belonging to the authenticated user.
    /// </summary>
    private static async Task<IResult> GetMyBudget(
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        if (!userId.HasValue)
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            var budget = await sender.Send(new GetMyBudgetQuery(userId.Value), cancellationToken);
            return budget is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(budget);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
    }

    /// <summary>
    /// Gets all user budgets for an authenticated manager.
    /// </summary>
    private static async Task<IResult> GetAllBudgets(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var budgets = await sender.Send(new GetAllBudgetsQuery(), cancellationToken);
        return TypedResults.Ok(budgets);
    }
}
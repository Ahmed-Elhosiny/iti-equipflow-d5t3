using EquipFlow.Application.Equipment.Queries;
using EquipFlow.Application.Equipment.Queries.Dtos;
using MediatR;

namespace EquipFlow.WebApi.Endpoints;

public static class MaintenanceEndpoints
{
    public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/equipment/{id:guid}/maintenance", GetMaintenanceHistory)
            .WithName("GetEquipmentMaintenanceHistory")
            .WithSummary("Get maintenance history for a specific equipment")
            .WithTags("Maintenance")
            .RequireAuthorization()
            .Produces<IReadOnlyList<EquipmentMaintenanceDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> GetMaintenanceHistory(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetEquipmentMaintenanceQuery(id);
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(exception.Message);
        }
    }
}
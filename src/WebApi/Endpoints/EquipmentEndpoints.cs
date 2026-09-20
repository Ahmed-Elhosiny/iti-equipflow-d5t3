using EquipFlow.Application.Equipment.Queries;
using EquipFlow.Application.Equipment.Queries.Dtos;
using MediatR;

namespace EquipFlow.WebApi.Endpoints;

public static class EquipmentEndpoints
{
    public static IEndpointRouteBuilder MapEquipmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/equipment", GetAll)
            .WithName("GetEquipment")
            .WithSummary("Get a list of equipment, optionally filtered by production line")
            .WithTags("Equipment")
            .RequireAuthorization()
            .Produces<IReadOnlyList<EquipmentDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/equipment/{id:guid}", GetById)
            .WithName("GetEquipmentById")
            .WithSummary("Get equipment details by ID")
            .WithTags("Equipment")
            .RequireAuthorization()
            .Produces<EquipmentDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> GetAll(
        string? line,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetEquipmentQuery(line);
        var result = await sender.Send(query, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetEquipmentByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);
        
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}
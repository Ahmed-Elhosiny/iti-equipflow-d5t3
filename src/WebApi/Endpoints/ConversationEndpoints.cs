using System.Security.Claims;
using EquipFlow.Application.Conversations.Commands;
using EquipFlow.Application.Conversations.Queries;
using EquipFlow.Application.Conversations.Queries.Dtos;
using EquipFlow.WebApi.Middleware;
using MediatR;

namespace EquipFlow.WebApi.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations", GetMyConversations)
            .WithName("GetMyConversations")
            .WithSummary("Get the authenticated user's conversations")
            .WithTags("Conversations")
            .RequireAuthorization()
            .Produces<IReadOnlyList<ConversationSummaryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/conversations/{id:guid}", GetConversationById)
            .WithName("GetConversationById")
            .WithSummary("Get a specific conversation with its messages")
            .WithTags("Conversations")
            .RequireAuthorization()
            .Produces<ConversationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/conversations", StartConversation)
            .WithName("StartConversation")
            .WithSummary("Start a new conversation")
            .WithTags("Conversations")
            .RequireAuthorization()
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> GetMyConversations(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var conversations = await sender.Send(
            new GetMyConversationsQuery(userId.Value.ToString()),
            cancellationToken);
        
        return Results.Ok(conversations);
    }

    private static async Task<IResult> GetConversationById(
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
            var conversation = await sender.Send(
                new GetConversationByIdQuery(id, userId.Value.ToString()),
                cancellationToken);
            
            return Results.Ok(conversation);
        }
        catch (InvalidOperationException)
        {
            // Fail closed with 404 to prevent IDOR / resource enumeration
            return Results.NotFound();
        }
    }

    private static async Task<IResult> StartConversation(
        StartConversationRequest request,
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

        if (string.IsNullOrWhiteSpace(httpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Correlation ID Missing");
        }

        try
        {
            var conversationId = await sender.Send(
                new StartConversationCommand(userId.Value.ToString(), request.Title),
                cancellationToken);
            
            return Results.Created($"/api/conversations/{conversationId}", conversationId);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }

    private sealed record StartConversationRequest(string Title);
}

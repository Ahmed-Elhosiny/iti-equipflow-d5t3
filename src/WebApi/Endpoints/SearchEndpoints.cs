using EquipFlow.Application.Search.Queries;
using EquipFlow.Domain.Search;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace EquipFlow.WebApi.Endpoints;

/// <summary>
/// Maps document search endpoints.
/// </summary>
public static class SearchEndpoints
{
    /// <summary>
    /// Maps the authenticated, role-restricted document search route.
    /// </summary>
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/search", Search)
            .WithName("SearchDocuments")
            .WithSummary("Search maintenance knowledge base using hybrid retrieval")
            .WithDescription("Executes a hybrid search (dense vector + keyword) across ingested maintenance documents. Supports metadata filtering by equipment, line, type, and version. Returns grounded chunks with citations, or a structured refusal if evidence is insufficient.")
            .WithTags("Search")
            .RequireAuthorization(policy => policy.RequireRole(
                "Technician",
                "Engineer",
                "Manager"));

        return endpoints;
    }

    private static async Task<IResult> Search(
        SearchRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var filters = SearchFilters.Create(
            request.EquipmentId,
            request.DocumentType,
            request.ProductionLine,
            request.DocumentVersion);

        var result = await sender.Send(
            new SearchDocumentsQuery(request.QueryText, request.TopK, filters),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    internal sealed record SearchRequest(
        string QueryText,
        int TopK = 10,
        string? EquipmentId = null,
        string? ProductionLine = null,
        string? DocumentType = null,
        string? DocumentVersion = null);
}

using EquipFlow.Application.Documents.Commands;
using EquipFlow.Application.Documents.Queries;
using EquipFlow.Application.Documents.Queries.Dtos;
using EquipFlow.Application.Features.Documents.Commands.IngestDocument;
using EquipFlow.Domain.Enums;
using EquipFlow.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EquipFlow.WebApi.Endpoints;

/// <summary>
/// Maps document ingestion and retrieval endpoints.
/// </summary>
public static class DocumentsEndpoints
{
    /// <summary>
    /// Maps the document endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/documents", IngestDocument)
            .WithName("IngestDocument")
            .WithTags("Documents")
            .WithSummary("Ingest a new document")
            .DisableAntiforgery()
            .RequireAuthorization("ManagerOnly");

        endpoints.MapGet("/api/documents", GetDocuments)
            .WithName("GetDocuments")
            .WithSummary("Get a list of all ingested documents")
            .WithTags("Documents")
            .RequireAuthorization()
            .Produces<IReadOnlyList<DocumentDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapGet("/api/documents/{id:guid}", GetDocumentById)
            .WithName("GetDocumentById")
            .WithSummary("Get document details by ID")
            .WithTags("Documents")
            .RequireAuthorization()
            .Produces<DocumentDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapDelete("/api/documents/{id:guid}", DeleteDocument)
            .WithName("DeleteDocument")
            .WithSummary("Delete a document and its chunks (Manager only)")
            .WithTags("Documents")
            .RequireAuthorization("ManagerOnly")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> GetDocuments(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetDocumentsQuery();
        var result = await sender.Send(query, cancellationToken);
        return Results.Ok(result);
    }
        private static async Task<IResult> GetDocumentById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetDocumentByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> DeleteDocument(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new DeleteDocumentCommand(id);
        var deleted = await sender.Send(command, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
    private static async Task<IResult> IngestDocument(
        IFormFile? file,
        [FromForm] DocumentType type,
        [FromForm] string? source,
        [FromForm] string? section,
        [FromForm] string? version,
        [FromForm] string? format,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return TypedResults.BadRequest("A non-empty file is required.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest("Only .pdf and .docx files are supported.");
        }

        try
        {
            using var fileStream = file.OpenReadStream();
            var documentId = await sender.Send(
                new IngestDocumentCommand
                {
                    FileStream = fileStream,
                    FileName = file.FileName,
                    Type = type,
                    Metadata = new DocumentMetadata(
                        source ?? string.Empty,
                        section ?? string.Empty,
                        null,
                        version ?? string.Empty,
                        format ?? string.Empty)
                },
                cancellationToken);

            return TypedResults.Created($"/api/documents/{documentId}", new { id = documentId });
        }
        catch (DocumentIngestionFailedException)
        {
            return TypedResults.UnprocessableEntity();
        }
    }
}
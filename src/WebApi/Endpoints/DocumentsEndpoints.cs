using EquipFlow.Application.Features.Documents.Commands.IngestDocument;
using EquipFlow.Domain.Enums;
using EquipFlow.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EquipFlow.WebApi.Endpoints;

/// <summary>
/// Maps document ingestion endpoints.
/// </summary>
public static class DocumentsEndpoints
{
    /// <summary>
    /// Maps the manager-protected document ingestion route.
    /// </summary>
    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/documents", IngestDocument)
            .WithName("IngestDocument")
            .DisableAntiforgery()
            .RequireAuthorization("ManagerOnly");

        return endpoints;
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
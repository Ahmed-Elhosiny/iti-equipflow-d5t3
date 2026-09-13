using EquipFlow.Application.Search.Models;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using EquipFlow.Domain.Search;
using EquipFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Search;

public sealed class PgVectorSearchAdapter : IVectorSearchPort
{
    private readonly EquipFlowDbContext _dbContext;

    public PgVectorSearchAdapter(EquipFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        SearchQuery query,
        ReadOnlyMemory<float> queryEmbedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        DocumentType? documentType = null;
        if (query.Filters.DocumentType is not null)
        {
            if (!Enum.TryParse<DocumentType>(
                query.Filters.DocumentType,
                ignoreCase: true,
                out var parsedDocumentType))
            {
                return [];
            }

            documentType = parsedDocumentType;
        }

        var queryVector = new Vector(queryEmbedding.ToArray());
        var candidates = _dbContext.DocumentChunks
            .AsNoTracking()
            .Join(
                _dbContext.Documents.AsNoTracking(),
                chunk => chunk.DocumentId,
                document => document.Id,
                (chunk, document) => new { Chunk = chunk, Document = document })
            .Where(candidate => candidate.Document.Status == DocumentStatus.Ready)
            .Where(candidate => EF.Property<Vector?>(
                candidate.Chunk,
                nameof(DocumentChunk.Embedding)) != null);

        // TODO: Replace these temporary mappings with explicit equipment, line, and version metadata fields for FR-013.
        if (query.Filters.EquipmentId is not null)
        {
            candidates = candidates.Where(candidate =>
                candidate.Document.Metadata.Source == query.Filters.EquipmentId);
        }

        if (query.Filters.DocumentType is not null)
        {
            candidates = candidates.Where(candidate =>
                candidate.Document.Type == documentType!.Value);
        }

        if (query.Filters.ProductionLine is not null)
        {
            candidates = candidates.Where(candidate =>
                candidate.Document.Metadata.Section == query.Filters.ProductionLine);
        }

        if (query.Filters.DocumentVersion is not null)
        {
            candidates = candidates.Where(candidate =>
                candidate.Document.Metadata.Version == query.Filters.DocumentVersion);
        }

        var rows = await candidates
            .Select(candidate => new
            {
                ChunkId = candidate.Chunk.Id,
                DocumentId = candidate.Chunk.DocumentId,
                Content = candidate.Chunk.Content,
                PageNumber = candidate.Chunk.Metadata.PageNumber,
                Section = candidate.Chunk.Metadata.Section,
                DocumentTitle = candidate.Document.Title,
                Distance = EF.Property<Vector>(
                    candidate.Chunk,
                    nameof(DocumentChunk.Embedding)).CosineDistance(queryVector)
            })
            .OrderBy(row => row.Distance)
            .Take(query.TopK * 3)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new RetrievedChunk(
                row.ChunkId,
                row.DocumentId,
                row.Content,
                1d - row.Distance,
                Citation.Create(
                    row.DocumentId,
                    row.DocumentTitle,
                    row.PageNumber is > 0 ? row.PageNumber : null,
                    row.Section)))
            .ToList();
    }
}
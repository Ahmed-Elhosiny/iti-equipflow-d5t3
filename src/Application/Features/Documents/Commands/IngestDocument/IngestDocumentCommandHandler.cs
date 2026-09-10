using EquipFlow.Application.Ports;
using EquipFlow.Domain.Entities;
using Microsoft.Extensions.Logging;
using MediatR;

namespace EquipFlow.Application.Features.Documents.Commands.IngestDocument;

public sealed class IngestDocumentCommandHandler
    : IRequestHandler<IngestDocumentCommand, Guid>
{
    private readonly IDocumentExtractor _extractor;
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingPort _embeddingPort;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _documentChunkRepository;
    private readonly ILogger<IngestDocumentCommandHandler> _logger;

    public IngestDocumentCommandHandler(
        IDocumentExtractor extractor,
        ITextChunker chunker,
        IEmbeddingPort embeddingPort,
        IDocumentRepository documentRepository,
        IDocumentChunkRepository documentChunkRepository,
        ILogger<IngestDocumentCommandHandler> logger)
    {
        _extractor = extractor;
        _chunker = chunker;
        _embeddingPort = embeddingPort;
        _documentRepository = documentRepository;
        _documentChunkRepository = documentChunkRepository;
        _logger = logger;
    }

    public async Task<Guid> Handle(
        IngestDocumentCommand request,
        CancellationToken cancellationToken)
    {
        Document? document = null;

        try
        {
            var rawText = await _extractor.ExtractTextAsync(
                request.FileStream,
                request.FileName,
                cancellationToken);
            var cleanedText = string.Join(' ', rawText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var chunks = _chunker.ChunkText(cleanedText);
            var embeddings = await _embeddingPort.GenerateEmbeddingsAsync(
                chunks,
                cancellationToken);

            if (embeddings.Length != chunks.Count)
                throw new InvalidOperationException("The embedding count must match the chunk count.");

            document = new Document(request.FileName, request.Type, request.Metadata);
            document.MarkAsProcessing();

            var documentChunks = new List<DocumentChunk>(chunks.Count);
            for (var index = 0; index < chunks.Count; index++)
            {
                var chunk = new DocumentChunk(
                    document.Id,
                    chunks[index],
                    request.Metadata,
                    chunks[index].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);
                chunk.SetEmbedding(embeddings[index]);
                document.AddChunk(chunk);
                documentChunks.Add(chunk);
            }

            await _documentRepository.AddAsync(document, cancellationToken);
            await _documentChunkRepository.AddRangeAsync(documentChunks, cancellationToken);

            document.MarkAsReady();
            await _documentRepository.UpdateAsync(document, cancellationToken);

            return document.Id;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Document ingestion failed for {FileName}.", request.FileName);

            if (document is not null)
            {
                document.MarkAsFailed(exception.Message);
                await _documentRepository.UpdateAsync(document, cancellationToken);
            }

            throw new DocumentIngestionFailedException(request.FileName, exception);
        }
    }
}
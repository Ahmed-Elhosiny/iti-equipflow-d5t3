using EquipFlow.Domain.Search;

namespace EquipFlow.Application.Search.Models;

public sealed record RetrievedChunk
{
    public Guid ChunkId { get; }

    public Guid DocumentId { get; }

    public string Content { get; }

    public double Score { get; }

    public Citation Citation { get; }

    public RetrievedChunk(
        Guid chunkId,
        Guid documentId,
        string content,
        double score,
        Citation citation)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be empty or whitespace.", nameof(content));
        }

        if (!double.IsFinite(score))
        {
            throw new ArgumentException("Score must be finite.", nameof(score));
        }

        ArgumentNullException.ThrowIfNull(citation);

        ChunkId = chunkId;
        DocumentId = documentId;
        Content = content;
        Score = score;
        Citation = citation;
    }
}
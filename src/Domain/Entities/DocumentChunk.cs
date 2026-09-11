using EquipFlow.Domain.ValueObjects;

namespace EquipFlow.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Content { get; private set; }
    public DocumentMetadata Metadata { get; private set; }
    public int TokenCount { get; private set; }
    public ReadOnlyMemory<float> Embedding { get; private set; }

    // EF Core parameterless constructor
    private DocumentChunk()
    {
        Id = Guid.Empty;
        DocumentId = Guid.Empty;
        Content = string.Empty;
        Metadata = null!;
        Embedding = ReadOnlyMemory<float>.Empty;
    }

    public DocumentChunk(Guid documentId, string content, DocumentMetadata metadata, int tokenCount)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId cannot be empty.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));
        ArgumentNullException.ThrowIfNull(metadata);
        if (tokenCount < 0)
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "TokenCount cannot be negative.");

        Id = Guid.NewGuid();
        DocumentId = documentId;
        Content = content;
        Metadata = metadata;
        TokenCount = tokenCount;
        Embedding = ReadOnlyMemory<float>.Empty;
    }

    public void SetEmbedding(ReadOnlyMemory<float> embedding)
    {
        Embedding = embedding;
    }
}
using EquipFlow.Domain.Enums;
using EquipFlow.Domain.ValueObjects;

namespace EquipFlow.Domain.Entities;

public class Document
{
    private readonly List<DocumentChunk> _chunks = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public DocumentType Type { get; private set; }
    public DocumentStatus Status { get; private set; }
    public DocumentMetadata Metadata { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyList<DocumentChunk> Chunks => _chunks;

    // EF Core parameterless constructor
    private Document()
    {
        Id = Guid.Empty;
        Title = string.Empty;
        Metadata = null!;
        Status = DocumentStatus.Pending;
        CreatedAt = DateTime.MinValue;
        UpdatedAt = DateTime.MinValue;
    }

    public Document(string title, DocumentType type, DocumentMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        ArgumentNullException.ThrowIfNull(metadata);

        Id = Guid.NewGuid();
        Title = title;
        Type = type;
        Metadata = metadata;
        Status = DocumentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void MarkAsProcessing()
    {
        Status = DocumentStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddChunk(DocumentChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        _chunks.Add(chunk);
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsReady()
    {
        Status = DocumentStatus.Ready;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));

        Status = DocumentStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }
}
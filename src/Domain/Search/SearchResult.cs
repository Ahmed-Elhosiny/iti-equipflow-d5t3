namespace EquipFlow.Domain.Search;

public sealed record SearchResult
{
    public Guid ChunkId { get; }

    public Guid DocumentId { get; }

    public string Content { get; }

    public double Score { get; }

    public int Rank { get; }

    public Citation Citation { get; }

    private SearchResult(
        Guid chunkId,
        Guid documentId,
        string content,
        double score,
        int rank,
        Citation citation)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be empty or whitespace.", nameof(content));
        }

        if (double.IsNaN(score))
        {
            throw new ArgumentException("Score cannot be NaN.", nameof(score));
        }

        if (rank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be greater than 0.");
        }

        Citation = citation ?? throw new ArgumentNullException(nameof(citation));
        ChunkId = chunkId;
        DocumentId = documentId;
        Content = content;
        Score = score;
        Rank = rank;
    }

    public static SearchResult Create(
        Guid chunkId,
        Guid documentId,
        string content,
        double score,
        int rank,
        Citation citation) =>
        new(chunkId, documentId, content, score, rank, citation);
}
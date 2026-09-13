namespace EquipFlow.Domain.Search;

public sealed record Citation
{
    public Guid DocumentId { get; }

    public string DocumentTitle { get; }

    public int? Page { get; }

    public string? Section { get; }

    private Citation(Guid documentId, string documentTitle, int? page, string? section)
    {
        if (documentTitle is null)
        {
            throw new ArgumentNullException(nameof(documentTitle));
        }

        if (string.IsNullOrWhiteSpace(documentTitle))
        {
            throw new ArgumentException("Document title cannot be empty or whitespace.", nameof(documentTitle));
        }

        if (page is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be greater than 0.");
        }

        DocumentId = documentId;
        DocumentTitle = documentTitle;
        Page = page;
        Section = section;
    }

    public static Citation Create(
        Guid documentId,
        string documentTitle,
        int? page = null,
        string? section = null) =>
        new(documentId, documentTitle, page, section);
}
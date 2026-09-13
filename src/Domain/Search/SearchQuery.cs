namespace EquipFlow.Domain.Search;

public sealed record SearchQuery
{
    public string QueryText { get; }

    public int TopK { get; }

    public SearchFilters Filters { get; }

    private SearchQuery(string queryText, int topK, SearchFilters? filters)
    {
        if (queryText is null)
        {
            throw new ArgumentNullException(nameof(queryText));
        }

        if (string.IsNullOrWhiteSpace(queryText))
        {
            throw new ArgumentException("Query text cannot be empty or whitespace.", nameof(queryText));
        }

        if (topK is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "TopK must be between 1 and 50.");
        }

        QueryText = queryText;
        TopK = topK;
        Filters = filters ?? SearchFilters.Create();
    }

    public static SearchQuery Create(
        string queryText,
        int topK = 10,
        SearchFilters? filters = null) =>
        new(queryText, topK, filters);
}
using EquipFlow.Domain.Search;
using MediatR;

namespace EquipFlow.Application.Search.Queries;

public sealed record SearchDocumentsQuery(
    string QueryText,
    int TopK = 10,
    SearchFilters? Filters = null) : IRequest<SearchDocumentsQueryResult>;

public sealed record SearchDocumentsQueryResult(
    IReadOnlyList<SearchResult> Results,
    bool IsRefusal,
    string? RefusalReason)
{
    public static SearchDocumentsQueryResult Success(IReadOnlyList<SearchResult> results) =>
        new(results, false, null);

    public static SearchDocumentsQueryResult Refused(string reason) =>
        new(Array.Empty<SearchResult>(), true, reason);
}
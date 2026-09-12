using EquipFlow.Domain.Search;

namespace EquipFlow.Application.Search.Ports;

public interface ISearchPort
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default);
}
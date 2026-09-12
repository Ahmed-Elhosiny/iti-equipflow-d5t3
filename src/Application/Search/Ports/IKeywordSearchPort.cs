using EquipFlow.Application.Search.Models;
using EquipFlow.Domain.Search;

namespace EquipFlow.Application.Search.Ports;

public interface IKeywordSearchPort
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default);
}
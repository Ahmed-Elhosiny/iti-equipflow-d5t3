using EquipFlow.Application.Search.Models;
using EquipFlow.Domain.Search;

namespace EquipFlow.Application.Search.Ports;

public interface IRerankerPort
{
    Task<IReadOnlyList<RetrievedChunk>> RerankAsync(
        SearchQuery query,
        IReadOnlyList<RetrievedChunk> candidates,
        CancellationToken cancellationToken = default);
}
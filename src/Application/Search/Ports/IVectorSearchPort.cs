using EquipFlow.Application.Search.Models;
using EquipFlow.Domain.Search;

namespace EquipFlow.Application.Search.Ports;

public interface IVectorSearchPort
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        SearchQuery query,
        ReadOnlyMemory<float> queryEmbedding,
        CancellationToken cancellationToken = default);
}
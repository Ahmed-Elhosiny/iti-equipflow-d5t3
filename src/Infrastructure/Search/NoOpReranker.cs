using EquipFlow.Application.Search.Models;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Domain.Search;

namespace EquipFlow.Infrastructure.Search;

internal sealed class NoOpReranker : IRerankerPort
{
    public Task<IReadOnlyList<RetrievedChunk>> RerankAsync(
        SearchQuery query,
        IReadOnlyList<RetrievedChunk> candidates,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(candidates);
}

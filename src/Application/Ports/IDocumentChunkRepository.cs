using EquipFlow.Domain.Entities;

namespace EquipFlow.Application.Ports;

public interface IDocumentChunkRepository
{
    Task AddRangeAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken);
}
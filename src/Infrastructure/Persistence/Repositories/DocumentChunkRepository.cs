using EquipFlow.Application.Ports;
using EquipFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class DocumentChunkRepository(EquipFlowDbContext context) : IDocumentChunkRepository
{
    public async Task AddRangeAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        await context.DocumentChunks.AddRangeAsync(chunks, cancellationToken);
    }
}
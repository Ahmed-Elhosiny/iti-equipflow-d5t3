using EquipFlow.Application.Ports;
using EquipFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository(EquipFlowDbContext context) : IDocumentRepository
{
    public async Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        await context.Documents.AddAsync(document, cancellationToken);
    }

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Documents
            .Include(document => document.Chunks)
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

    public async Task UpdateAsync(Document document, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
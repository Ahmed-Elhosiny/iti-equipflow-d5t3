using EquipFlow.Application.Ports;
using EquipFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class EquipmentRepository(EquipFlowDbContext context) : IEquipmentRepository
{
    public async Task<Equipment?> GetByIdAsync(
        Guid equipmentId,
        CancellationToken cancellationToken = default) =>
        await context.Equipments.FindAsync([equipmentId], cancellationToken);

    public async Task<IReadOnlyList<Equipment>> GetAllAsync(
        string? line,
        CancellationToken cancellationToken = default)
    {
        var query = context.Equipments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(line))
        {
            query = query.Where(e => e.Line == line);
        }

        return await query.OrderBy(e => e.Name).ToListAsync(cancellationToken);
    }
}
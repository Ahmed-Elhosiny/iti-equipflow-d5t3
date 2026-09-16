using EquipFlow.Application.Ports;
using EquipFlow.Domain;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class EquipmentRepository(EquipFlowDbContext context) : IEquipmentRepository
{
    public async Task<Equipment?> GetByIdAsync(
        Guid equipmentId,
        CancellationToken cancellationToken = default) =>
        await context.Equipments.FindAsync([equipmentId], cancellationToken);
}

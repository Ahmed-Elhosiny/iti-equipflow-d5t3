using EquipFlow.Domain;

namespace EquipFlow.Application.Ports;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(Guid equipmentId, CancellationToken cancellationToken = default);
}

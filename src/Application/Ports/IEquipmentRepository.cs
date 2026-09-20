using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EquipFlow.Application.Ports;

public interface IEquipmentRepository
{
    Task<global::EquipFlow.Domain.Equipment?> GetByIdAsync(
        Guid equipmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<global::EquipFlow.Domain.Equipment>> GetAllAsync(
        string? line,
        CancellationToken cancellationToken = default);
}
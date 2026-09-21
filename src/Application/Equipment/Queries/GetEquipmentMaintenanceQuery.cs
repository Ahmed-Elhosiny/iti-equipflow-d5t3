using EquipFlow.Application.Equipment.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.Equipment.Queries;

public sealed record GetEquipmentMaintenanceQuery(Guid EquipmentId) 
    : IRequest<IReadOnlyList<EquipmentMaintenanceDto>>;
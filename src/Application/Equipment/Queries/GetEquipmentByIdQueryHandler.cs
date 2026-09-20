using EquipFlow.Application.Equipment.Queries.Dtos;
using EquipFlow.Application.Ports;
using MediatR;

namespace EquipFlow.Application.Equipment.Queries;

public sealed class GetEquipmentByIdQueryHandler(IEquipmentRepository repository)
    : IRequestHandler<GetEquipmentByIdQuery, EquipmentDto?>
{
    public async Task<EquipmentDto?> Handle(
        GetEquipmentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var equipment = await repository.GetByIdAsync(request.Id, cancellationToken);
        
        return equipment is null ? null : new EquipmentDto(
            equipment.Id,
            equipment.Name,
            equipment.SerialNumber,
            equipment.Line);
    }
}
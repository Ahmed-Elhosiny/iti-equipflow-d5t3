using EquipFlow.Application.Equipment.Queries.Dtos;
using EquipFlow.Application.Ports;
using MediatR;

namespace EquipFlow.Application.Equipment.Queries;

public sealed class GetEquipmentQueryHandler(IEquipmentRepository repository)
    : IRequestHandler<GetEquipmentQuery, IReadOnlyList<EquipmentDto>>
{
    public async Task<IReadOnlyList<EquipmentDto>> Handle(
        GetEquipmentQuery request,
        CancellationToken cancellationToken)
    {
        var equipmentList = await repository.GetAllAsync(request.Line, cancellationToken);
        
        return equipmentList.Select(e => new EquipmentDto(
            e.Id,
            e.Name,
            e.SerialNumber,
            e.Line)).ToList();
    }
}
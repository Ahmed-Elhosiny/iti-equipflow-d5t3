using EquipFlow.Application.Equipment.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.Equipment.Queries;

public sealed record GetEquipmentByIdQuery(Guid Id) : IRequest<EquipmentDto?>;
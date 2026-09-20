using EquipFlow.Application.Equipment.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.Equipment.Queries;

public sealed record GetEquipmentQuery(string? Line) : IRequest<IReadOnlyList<EquipmentDto>>;
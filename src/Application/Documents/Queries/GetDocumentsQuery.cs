using EquipFlow.Application.Documents.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.Documents.Queries;

public sealed record GetDocumentsQuery : IRequest<IReadOnlyList<DocumentDto>>;
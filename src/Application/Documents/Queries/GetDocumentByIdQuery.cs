using EquipFlow.Application.Documents.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.Documents.Queries;

public sealed record GetDocumentByIdQuery(Guid Id) : IRequest<DocumentDto?>;

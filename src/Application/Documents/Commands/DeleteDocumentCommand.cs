using MediatR;

namespace EquipFlow.Application.Documents.Commands;

public sealed record DeleteDocumentCommand(Guid Id) : IRequest<bool>;

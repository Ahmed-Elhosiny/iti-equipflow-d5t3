using EquipFlow.Domain.Enums;
using EquipFlow.Domain.ValueObjects;
using MediatR;

namespace EquipFlow.Application.Features.Documents.Commands.IngestDocument;

public sealed class IngestDocumentCommand : IRequest<Guid>
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required DocumentType Type { get; init; }
    public required DocumentMetadata Metadata { get; init; }
}
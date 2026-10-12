using EquipFlow.Application.Documents.Queries.Dtos;
using EquipFlow.Application.Ports;
using MediatR;

namespace EquipFlow.Application.Documents.Queries;

public sealed class GetDocumentByIdQueryHandler(IDocumentRepository repository)
    : IRequestHandler<GetDocumentByIdQuery, DocumentDto?>
{
    public async Task<DocumentDto?> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (document is null) return null;

        return new DocumentDto(
            document.Id,
            document.Title,
            document.Type.ToString(),
            document.Status.ToString(),
            document.Metadata.Source,
            document.Metadata.Section,
            document.Metadata.Version,
            document.Metadata.Format,
            document.CreatedAt);
    }
}

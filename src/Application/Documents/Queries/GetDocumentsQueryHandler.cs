using EquipFlow.Application.Documents.Queries.Dtos;
using EquipFlow.Application.Ports;
using MediatR;

namespace EquipFlow.Application.Documents.Queries;

public sealed class GetDocumentsQueryHandler(IDocumentRepository repository)
    : IRequestHandler<GetDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(
        GetDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var documents = await repository.GetAllAsync(cancellationToken);
        
        return documents.Select(d => new DocumentDto(
            d.Id,
            d.Title,
            d.Type.ToString(),
            d.Status.ToString(),
            d.Metadata.Source,
            d.Metadata.Section,
            d.Metadata.Version,
            d.Metadata.Format,
            d.CreatedAt)).ToList();
    }
}
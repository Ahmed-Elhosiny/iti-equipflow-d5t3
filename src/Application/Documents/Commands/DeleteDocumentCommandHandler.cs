using EquipFlow.Application.Ports;
using MediatR;

namespace EquipFlow.Application.Documents.Commands;

public sealed class DeleteDocumentCommandHandler(IDocumentRepository repository)
    : IRequestHandler<DeleteDocumentCommand, bool>
{
    public async Task<bool> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (document is null) return false;

        await repository.DeleteAsync(document, cancellationToken);
        return true;
    }
}

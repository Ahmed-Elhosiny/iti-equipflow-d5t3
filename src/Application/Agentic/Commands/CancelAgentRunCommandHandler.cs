using EquipFlow.Application.Agentic.Abstractions;
using MediatR;

namespace EquipFlow.Application.Agentic.Commands;

public sealed class CancelAgentRunCommandHandler(IActiveRunRegistry registry)
    : IRequestHandler<CancelAgentRunCommand, bool>
{
    public Task<bool> Handle(CancelAgentRunCommand request, CancellationToken cancellationToken)
    {
        var cancelled = registry.TryCancel(request.RunId);
        return Task.FromResult(cancelled);
    }
}
using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.CostGovernor.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.CostGovernor.Queries;

/// <summary>
/// Handles the retrieval of run spend history for a specific user.
/// </summary>
public sealed class GetMySpendQueryHandler(IRunSpendRepository runSpendRepository)
    : IRequestHandler<GetMySpendQuery, IEnumerable<RunHistoryDto>>
{
    public async Task<IEnumerable<RunHistoryDto>> Handle(
        GetMySpendQuery request,
        CancellationToken cancellationToken)
    {
        var spends = await runSpendRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        
        return spends.Select(spend => new RunHistoryDto(
            RunId: spend.RunId,
            Timestamp: spend.CreatedAtUtc.UtcDateTime,
            // RunSpend tracks ModelUsed rather than AgentName directly; 
            // we map ModelUsed here as the closest available descriptor for the run context.
            AgentName: spend.ModelUsed, 
            EstimatedCost: spend.ReservedAmount.Amount,
            ActualCost: spend.ActualAmount.Amount,
            Status: spend.ActualAmount.Amount > 0 ? "Completed" : "Reserved"
        )).OrderByDescending(dto => dto.Timestamp).ToList();
    }
}
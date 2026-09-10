namespace EquipFlow.Application.Ports;

public interface IModelRouter
{
    Task<ModelRoute?> GetCheaperModelAsync(
        decimal currentPricePerThousandTokens,
        CancellationToken cancellationToken = default);
}

public sealed record ModelRoute(
    string ModelName,
    decimal PricePerThousandTokens);

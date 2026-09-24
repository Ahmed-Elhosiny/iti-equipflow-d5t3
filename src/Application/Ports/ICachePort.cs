namespace EquipFlow.Application.Ports;

public interface ICachePort
{
    Task<SemanticCacheMatch?> FindSemanticMatchAsync(
        string? semanticQuery,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        string query, 
        string response, 
        CancellationToken cancellationToken = default);
}

public sealed record SemanticCacheMatch(
    string Key,
    string Response);
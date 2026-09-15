using EquipFlow.Application.Tools.Definitions;

namespace EquipFlow.Application.Tools.Ports;

/// <summary>
/// Searches equipment manuals through the application's retrieval boundary.
/// </summary>
public interface ISearchManualsTool
{
    /// <summary>
    /// Executes a manual search and returns grounded chunks with citations.
    /// </summary>
    Task<SearchManualsResponse> ExecuteAsync(
        SearchManualsRequest request,
        CancellationToken cancellationToken = default);
}
namespace EquipFlow.Application.Tools.Ports;

/// <summary>
/// Executes a specific tool invocation.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Gets the exact name of the tool handled by this executor.
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Executes a tool invocation.
    /// </summary>
    /// <param name="request">The tool invocation to execute.</param>
    /// <param name="cancellationToken">The token used to cancel execution.</param>
    /// <returns>The result of the tool execution.</returns>
    Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default);
}
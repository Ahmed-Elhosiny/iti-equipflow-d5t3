using EquipFlow.Application.Tools.Ports;

namespace EquipFlow.Infrastructure.Tools.Dispatcher;

/// <summary>
/// Dispatches tool invocations to registered tool executors.
/// </summary>
public sealed class ToolDispatcher : IToolDispatcher
{
    private readonly Dictionary<string, IToolExecutor> _executors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolDispatcher"/> class.
    /// </summary>
    /// <param name="executors">The tool executors available for dispatch.</param>
    /// <exception cref="ArgumentException">Thrown when duplicate tool names are registered.</exception>
    public ToolDispatcher(IEnumerable<IToolExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        _executors = new Dictionary<string, IToolExecutor>(StringComparer.OrdinalIgnoreCase);
        foreach (var executor in executors)
        {
            if (!_executors.TryAdd(executor.ToolName, executor))
            {
                throw new ArgumentException(
                    $"A tool executor is already registered for tool '{executor.ToolName}'.",
                    nameof(executors));
            }
        }
    }

    /// <inheritdoc />
    public async Task<ToolDispatchResult> DispatchAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_executors.TryGetValue(request.ToolName, out var executor))
        {
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ToolNotFound,
                null,
                "TOOL_NOT_FOUND",
                $"Tool '{request.ToolName}' was not found.");
        }

        try
        {
            return await executor.ExecuteAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "EXECUTOR_FAILED",
                exception.Message);
        }
    }
}

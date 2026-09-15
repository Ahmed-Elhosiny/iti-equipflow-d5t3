using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Dispatcher;

/// <summary>
/// Decorates tool dispatch with agent tool authorization for TL-007 and AG-003,
/// mitigating Excessive Agency by restricting the tools each agent may invoke.
/// </summary>
public sealed class AuthorizingToolDispatcher : IToolDispatcher
{
    private readonly IToolDispatcher _innerDispatcher;
    private readonly IAgentToolRegistry _agentToolRegistry;
    private readonly ILogger<AuthorizingToolDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizingToolDispatcher"/> class.
    /// </summary>
    /// <param name="innerDispatcher">The dispatcher to invoke after authorization succeeds.</param>
    /// <param name="agentToolRegistry">The registry used to determine whether an agent may invoke a tool.</param>
    /// <param name="logger">The logger used to record blocked tool invocations.</param>
    public AuthorizingToolDispatcher(
        IToolDispatcher innerDispatcher,
        IAgentToolRegistry agentToolRegistry,
        ILogger<AuthorizingToolDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(innerDispatcher);
        ArgumentNullException.ThrowIfNull(agentToolRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _innerDispatcher = innerDispatcher;
        _agentToolRegistry = agentToolRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ToolDispatchResult> DispatchAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_agentToolRegistry.IsToolAllowed(request.Context.AgentName, request.ToolName))
        {
            var errorMessage =
                $"Agent '{request.Context.AgentName}' is not permitted to invoke tool '{request.ToolName}'.";

            _logger.LogWarning(
                "Tool invocation blocked because agent is not authorized for the tool. AgentName: {AgentName}, ToolName: {ToolName}, ErrorCode: {ErrorCode}",
                request.Context.AgentName,
                request.ToolName,
                "AGENT_TOOL_NOT_ALLOWED");

            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.BlockedByAgentToolAllowList,
                null,
                "AGENT_TOOL_NOT_ALLOWED",
                errorMessage);
        }

        return await _innerDispatcher.DispatchAsync(request, cancellationToken);
    }
}
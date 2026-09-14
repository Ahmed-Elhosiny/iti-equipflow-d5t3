namespace EquipFlow.Application.Tools.Ports;

/// <summary>
/// Describes the outcome of a tool dispatch attempt.
/// </summary>
public enum ToolDispatchStatus
{
    /// <summary>
    /// The tool executed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Dispatch was blocked because validation failed.
    /// </summary>
    BlockedByValidation,

    /// <summary>
    /// Dispatch was blocked because the caller was not authorized.
    /// </summary>
    BlockedByAuthorization,

    /// <summary>
    /// Dispatch was blocked because the tool is not allowed for the agent.
    /// </summary>
    BlockedByAgentToolAllowList,

    /// <summary>
    /// Dispatch was blocked because the budget was exceeded.
    /// </summary>
    BlockedByBudget,

    /// <summary>
    /// Dispatch was blocked because a safety gate was not satisfied.
    /// </summary>
    BlockedBySafetyGate,

    /// <summary>
    /// The tool requires approval before it can execute.
    /// </summary>
    ApprovalRequired,

    /// <summary>
    /// The requested tool was not found.
    /// </summary>
    ToolNotFound,

    /// <summary>
    /// The tool executor failed.
    /// </summary>
    ExecutorFailed,

    /// <summary>
    /// The dispatch outcome is unknown.
    /// </summary>
    Unknown
}

/// <summary>
/// Identifies the caller and execution context for a tool invocation.
/// </summary>
/// <param name="UserId">The identifier of the user initiating the invocation.</param>
/// <param name="UserRole">The role of the user initiating the invocation.</param>
/// <param name="RunId">The identifier of the agent run.</param>
/// <param name="AgentStepId">The identifier of the agent step requesting the tool.</param>
/// <param name="AgentName">The name of the agent requesting the tool.</param>
/// <param name="CorrelationId">The identifier used to correlate related operations.</param>
public sealed record ToolInvocationContext(
    Guid UserId,
    string UserRole,
    Guid RunId,
    Guid AgentStepId,
    string AgentName,
    string CorrelationId);

/// <summary>
/// Describes a tool invocation to dispatch.
/// </summary>
/// <param name="ToolName">The name of the tool to invoke.</param>
/// <param name="ArgumentsJson">The JSON arguments for the tool.</param>
/// <param name="Context">The caller and execution context for the invocation.</param>
public sealed record ToolInvocationRequest(
    string ToolName,
    string ArgumentsJson,
    ToolInvocationContext Context);

/// <summary>
/// Contains the outcome of a dispatched tool invocation.
/// </summary>
/// <param name="ToolName">The name of the dispatched tool.</param>
/// <param name="Succeeded">Indicates whether the tool invocation succeeded.</param>
/// <param name="Status">The status of the dispatch attempt.</param>
/// <param name="ResultJson">The JSON result returned by the tool, if available.</param>
/// <param name="ErrorCode">The error code, if the dispatch did not succeed.</param>
/// <param name="ErrorMessage">The error message, if the dispatch did not succeed.</param>
public sealed record ToolDispatchResult(
    string ToolName,
    bool Succeeded,
    ToolDispatchStatus Status,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// Dispatches validated tool invocations to their executors.
/// </summary>
public interface IToolDispatcher
{
    /// <summary>
    /// Dispatches a tool invocation.
    /// </summary>
    /// <param name="request">The tool invocation to dispatch.</param>
    /// <param name="cancellationToken">The token used to cancel the dispatch.</param>
    /// <returns>The result of the dispatch attempt.</returns>
    Task<ToolDispatchResult> DispatchAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default);
}
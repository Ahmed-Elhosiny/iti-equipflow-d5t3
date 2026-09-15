namespace EquipFlow.Application.Tools.Ports;

/// <summary>
/// Defines the Application-layer port for enforcing AG-003 (Restricted Tools)
/// and mitigating Excessive Agency by controlling which tools agents may invoke.
/// </summary>
public interface IAgentToolRegistry
{
    /// <summary>
    /// Determines whether the specified agent is permitted to invoke the specified tool.
    /// </summary>
    /// <param name="agentName">The name of the agent requesting access.</param>
    /// <param name="toolName">The name of the tool to invoke.</param>
    /// <returns><see langword="true"/> when the agent is permitted to invoke the tool; otherwise, <see langword="false"/>.</returns>
    bool IsToolAllowed(string agentName, string toolName);

    /// <summary>
    /// Gets the complete list of tools the specified agent is permitted to invoke.
    /// </summary>
    /// <param name="agentName">The name of the agent whose permitted tools are requested.</param>
    /// <returns>The complete list of permitted tool names for the specified agent.</returns>
    IReadOnlyList<string> GetAllowedTools(string agentName);
}

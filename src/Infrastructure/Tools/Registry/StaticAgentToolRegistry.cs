using EquipFlow.Application.Tools.Ports;

namespace EquipFlow.Infrastructure.Tools.Registry;

/// <summary>
/// Provides the MVP agent tool allow-lists used to enforce AG-003 Restricted Tools.
/// </summary>
public sealed class StaticAgentToolRegistry : IAgentToolRegistry
{
    private readonly Dictionary<string, HashSet<string>> _allowedTools;

    /// <summary>
    /// Initializes the MVP agent tool allow-lists.
    /// </summary>
    public StaticAgentToolRegistry()
    {
        _allowedTools = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SymptomMatcher"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SearchManuals",
                "QueryFaultHistory"
            },
            ["DiagnosticSafetyPlanner"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SearchManuals",
                "GetEquipmentSpecs",
                "GenerateSafetyChecklist"
            },
            ["WorkOrderGenerator"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CreateWorkOrder",
                "ValidateBudget"
            }
        };
    }

    /// <inheritdoc />
    public bool IsToolAllowed(string agentName, string toolName)
    {
        return _allowedTools.TryGetValue(agentName, out var allowedTools)
            && allowedTools.Contains(toolName);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllowedTools(string agentName)
    {
        return _allowedTools.TryGetValue(agentName, out var allowedTools)
            ? allowedTools.ToList()
            : Array.Empty<string>();
    }
}
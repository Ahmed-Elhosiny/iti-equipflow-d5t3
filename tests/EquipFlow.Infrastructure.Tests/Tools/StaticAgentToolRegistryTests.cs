using EquipFlow.Infrastructure.Tools.Registry;

namespace EquipFlow.Infrastructure.Tests.Tools;

public sealed class StaticAgentToolRegistryTests
{
    [Fact]
    public void SymptomMatcher_AllowsQueryFaultHistory()
    {
        var registry = new StaticAgentToolRegistry();

        Assert.True(registry.IsToolAllowed("SymptomMatcher", "QueryFaultHistory"));
    }

    [Fact]
    public void DiagnosticSafetyPlanner_AllowsQueryFaultHistory()
    {
        var registry = new StaticAgentToolRegistry();

        Assert.True(registry.IsToolAllowed("DiagnosticSafetyPlanner", "QueryFaultHistory"));
    }

    [Fact]
    public void WorkOrderGenerator_DoesNotAllowQueryFaultHistory()
    {
        var registry = new StaticAgentToolRegistry();

        Assert.False(registry.IsToolAllowed("WorkOrderGenerator", "QueryFaultHistory"));
    }
}

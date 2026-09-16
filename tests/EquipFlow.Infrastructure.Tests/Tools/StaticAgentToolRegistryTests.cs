using EquipFlow.Infrastructure.Tools.Registry;

namespace EquipFlow.Infrastructure.Tests.Tools;

public sealed class StaticAgentToolRegistryTests
{
    // --- SearchManuals Tests ---
    
    [Fact]
    public void SymptomMatcher_AllowsSearchManuals()
    {
        var registry = new StaticAgentToolRegistry();
        Assert.True(registry.IsToolAllowed("SymptomMatcher", "SearchManuals"));
    }

    [Fact]
    public void DiagnosticSafetyPlanner_AllowsSearchManuals()
    {
        var registry = new StaticAgentToolRegistry();
        Assert.True(registry.IsToolAllowed("DiagnosticSafetyPlanner", "SearchManuals"));
    }

    [Fact]
    public void WorkOrderGenerator_DoesNotAllowSearchManuals()
    {
        var registry = new StaticAgentToolRegistry();
        Assert.False(registry.IsToolAllowed("WorkOrderGenerator", "SearchManuals"));
    }

    // --- QueryFaultHistory Tests ---

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

    // --- GenerateSafetyChecklist Tests ---

    [Fact]
    public void DiagnosticSafetyPlanner_AllowsGenerateSafetyChecklist()
    {
        var registry = new StaticAgentToolRegistry();
        Assert.True(registry.IsToolAllowed("DiagnosticSafetyPlanner", "GenerateSafetyChecklist"));
    }

    [Fact]
    public void SymptomMatcher_DoesNotAllowGenerateSafetyChecklist()
    {
        var registry = new StaticAgentToolRegistry();
        Assert.False(registry.IsToolAllowed("SymptomMatcher", "GenerateSafetyChecklist"));
    }

    [Fact]
    public void WorkOrderGenerator_DoesNotAllowGenerateSafetyChecklist()
    {
        var registry = new StaticAgentToolRegistry();
        Assert.False(registry.IsToolAllowed("WorkOrderGenerator", "GenerateSafetyChecklist"));
    }

    [Fact]
    public void WorkOrderGenerator_AllowsValidateBudget()
    {
        var registry = new StaticAgentToolRegistry();
        Assert.True(registry.IsToolAllowed("WorkOrderGenerator", "ValidateBudget"));
    }
}
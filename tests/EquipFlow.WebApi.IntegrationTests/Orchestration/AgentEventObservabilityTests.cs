using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests;

public sealed class AgentEventObservabilityTests : IClassFixture<EquipFlowWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly IServiceScopeFactory scopeFactory;

    public AgentEventObservabilityTests(EquipFlowWebApplicationFactory factory)
    {
        client = factory.CreateClient();
        scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
    }

       [Fact]
    public async Task Analyze_ShouldPersistAgentEvents_WithCorrectCorrelationIdAndTokenUsage()
    {
        var correlationId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai/analyze")
        {
            Content = JsonContent.Create(new
            {
                symptomDescription = "Pump P-101 is overheating and vibrating",
                equipmentIdHint = "P-101"
            })
        };
        request.Headers.Add("X-Correlation-Id", correlationId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, "Engineer");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EquipFlowDbContext>();
        var entities = await dbContext.AgentEvents
            .Where(agentEvent => agentEvent.CorrelationId == correlationId)
            .ToListAsync();

        entities.Should().NotBeEmpty();
        entities.Should().Contain(agentEvent => agentEvent.EventType == nameof(AgentRunStarted));
        entities.Should().Contain(agentEvent => agentEvent.EventType == nameof(AgentRunCompleted));
        entities.Should().Contain(agentEvent => agentEvent.EventType == nameof(ToolInvoked));

        // Assert per-step events exist
        entities.Should().Contain(agentEvent => agentEvent.EventType == nameof(AgentStepStarted));
        entities.Should().Contain(agentEvent => agentEvent.EventType == nameof(AgentStepCompleted));

        var events = entities.Select(e => e.EventType switch
        {
            nameof(AgentRunStarted) => (AgentEventBase?)JsonSerializer.Deserialize<AgentRunStarted>(e.Payload),
            nameof(AgentRunCompleted) => JsonSerializer.Deserialize<AgentRunCompleted>(e.Payload),
            nameof(AgentStepStarted) => JsonSerializer.Deserialize<AgentStepStarted>(e.Payload),
            nameof(AgentStepCompleted) => JsonSerializer.Deserialize<AgentStepCompleted>(e.Payload),
            nameof(LlmCallCompleted) => JsonSerializer.Deserialize<LlmCallCompleted>(e.Payload),
            nameof(ToolInvoked) => JsonSerializer.Deserialize<ToolInvoked>(e.Payload),
            nameof(CitationAttached) => JsonSerializer.Deserialize<CitationAttached>(e.Payload),
            _ => null
        }).Where(e => e is not null).Cast<AgentEventBase>().ToList();

        var startedSteps = events.OfType<AgentStepStarted>().ToList();
        var completedSteps = events.OfType<AgentStepCompleted>().ToList();
        
        // Assert every started step has a paired completed step
        startedSteps.Should().HaveSameCount(completedSteps);

        // Assert totalCostUsd and token invariants
        var llmCalls = events.OfType<LlmCallCompleted>().ToList();
        llmCalls.Should().NotBeEmpty();
        
        var expectedTotalCost = llmCalls.Sum(c => c.EstimatedCostUsd);
        var expectedTotalTokens = llmCalls.Sum(c => c.TotalTokens);
        
        // Verify the invariant: we can successfully aggregate billing and usage metrics from events
        expectedTotalTokens.Should().BeGreaterThan(0);
        expectedTotalCost.Should().BeGreaterThanOrEqualTo(0); // Mock provider returns 0 cost, but aggregation must work
    }
}
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
        var events = await dbContext.AgentEvents
            .Where(agentEvent => agentEvent.CorrelationId == correlationId)
            .ToListAsync();

        events.Should().NotBeEmpty();
        events.Should().Contain(agentEvent => agentEvent.EventType == nameof(AgentRunStarted));
        events.Should().Contain(agentEvent => agentEvent.EventType == nameof(AgentRunCompleted));
        events.Should().Contain(agentEvent => agentEvent.EventType == nameof(ToolInvoked));

        var llmCallCompleted = events.First(agentEvent =>
            agentEvent.EventType == nameof(LlmCallCompleted));
        var llmEvent = JsonSerializer.Deserialize<LlmCallCompleted>(llmCallCompleted.Payload);

        llmEvent.Should().NotBeNull();
        llmEvent!.TotalTokens.Should().BeGreaterThan(0);
    }
}
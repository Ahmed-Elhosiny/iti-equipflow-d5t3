using System.Net;
using System.Net.Http.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests;

public sealed class AgentRunInspectionTests : IClassFixture<EquipFlowWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly IServiceScopeFactory scopeFactory;

    public AgentRunInspectionTests(EquipFlowWebApplicationFactory factory)
    {
        client = factory.CreateClient();
        scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task GetRunById_Returns200_WhenRunExists()
    {
        var correlationId = Guid.NewGuid();
        await SeedRunAsync(correlationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/runs/{correlationId}");
        request.Headers.Add(TestAuthHandler.RoleHeader, "Engineer");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RunInspectionResponse>();
        body.Should().NotBeNull();
        body!.CorrelationId.Should().Be(correlationId);
        body.Events.Should().HaveCount(2);
        body.Events.Should().Contain(eventItem => eventItem.EventType == nameof(AgentRunStarted));
        body.Events.Should().Contain(eventItem => eventItem.EventType == nameof(AgentRunCompleted));
    }

    [Fact]
    public async Task GetRunById_Returns404_WhenRunDoesNotExist()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/runs/{Guid.NewGuid()}");
        request.Headers.Add(TestAuthHandler.RoleHeader, "Engineer");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

      private async Task SeedRunAsync(Guid correlationId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IAgentEventStore>();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        
        // The TestAuthHandler authenticates as this specific user ID.
        // We must seed the run with this exact UserId so the object-level 
        // authorization check in GetAgentRunByIdQueryHandler passes.
        var testUserId = "00000000-0000-0000-0000-000000000001";

        await eventStore.AppendRangeAsync(
        [
            new AgentRunStarted(
                correlationId,
                startedAt,
                "InspectionTestAgent",
                0,
                "test run",
                120000,
                testUserId), // <-- Explicitly pass the test user ID
            new AgentRunCompleted(
                correlationId,
                startedAt.AddMinutes(1),
                "InspectionTestAgent",
                1,
                AgentRunStatus.Success,
                60000,
                null,
                "completed")
        ]);
    }
    private sealed record RunInspectionResponse(
        Guid CorrelationId,
        string? Status,
        DateTime StartedAt,
        DateTime? EndedAt,
        List<RunEventResponse> Events);

    private sealed record RunEventResponse(
        string EventType,
        DateTime Timestamp,
        string AgentName,
        string SerializedData,
        bool? Success);
}
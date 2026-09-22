using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests.Endpoints;

public sealed class ChatEndpointTests : IClassFixture<EquipFlowWebApplicationFactory>
{
    private readonly HttpClient client;

    public ChatEndpointTests(EquipFlowWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Chat_ShouldReturnSSEStream_WithProgressAndTokenEvents_WhenFullChainSucceeds()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                message = "Pump P-101 is overheating and vibrating",
                equipmentContext = "P-101"
            })
        };
        request.Headers.Add(TestAuthHandler.RoleHeader, "Engineer");

        // Act
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        var rawSse = await reader.ReadToEndAsync();

        // Verify SSE event structure and content
        rawSse.Should().Contain("event: agent.started");
        rawSse.Should().Contain("event: token");
        rawSse.Should().Contain("event: done");
        rawSse.Should().Contain("PendingApproval");
    }

    [Fact]
    public async Task Chat_ShouldReturn401_WhenUnauthenticated()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new { message = "Test" })
        };
        // No auth header provided

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Chat_ShouldCancel_WhenClientDisconnectsEarly()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new { message = "Pump P-101 is overheating" })
        };
        request.Headers.Add(TestAuthHandler.RoleHeader, "Engineer");

        // Act & Assert
        var act = async () =>
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);
            // This will throw when the cancellation token is triggered
            await reader.ReadToEndAsync(cts.Token);
        };

        await act.Should().ThrowAsync<Exception>()
            .Where(ex => ex is OperationCanceledException || ex is TaskCanceledException || ex is HttpRequestException);
    }
}
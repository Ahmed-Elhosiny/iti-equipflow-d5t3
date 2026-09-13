using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using EquipFlow.Application.Search.Queries;
using EquipFlow.Domain.Search;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests.Endpoints;

public sealed class SearchEndpointTests : IClassFixture<SearchWebApplicationFactory>
{
    private readonly HttpClient client;

    public SearchEndpointTests(SearchWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_WithAuthorizedRole_ReturnsResultsAndCitations()
    {
        using var request = CreateRequest("hydraulic pump", "Engineer");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchResponse>();
        body.Should().NotBeNull();
        body!.IsRefusal.Should().BeFalse();
        body.RefusalReason.Should().BeNull();
        var result = body.Results.Should().ContainSingle().Subject;
        result.Content.Should().Be("Hydraulic pump maintenance procedure.");
        result.Score.Should().Be(0.92);
        result.Rank.Should().Be(1);
        result.Citation.DocumentTitle.Should().Be("Pump Maintenance Manual");
        result.Citation.Page.Should().Be(12);
        result.Citation.Section.Should().Be("Hydraulics");
    }

    [Fact]
    public async Task Search_WhenApplicationRefuses_ReturnsRefusalPayload()
    {
        using var request = CreateRequest("missing manual", "Technician");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchResponse>();
        body.Should().NotBeNull();
        body!.IsRefusal.Should().BeTrue();
        body.Results.Should().BeEmpty();
        body.RefusalReason.Should().Be("No sufficiently relevant documents found.");
    }

    [Fact]
    public async Task Search_WithoutAuthorization_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/search")
        {
            Content = JsonContent.Create(new { queryText = "hydraulic pump", topK = 3 })
        };

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_WithUnauthorizedRole_ReturnsForbidden()
    {
        using var request = CreateRequest("hydraulic pump", "ReadOnly");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static HttpRequestMessage CreateRequest(string queryText, string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/search")
        {
            Content = JsonContent.Create(new { queryText, topK = 3 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            SearchWebApplicationFactory.CreateToken(role));
        return request;
    }

    private sealed record SearchResponse(
        IReadOnlyList<SearchResultResponse> Results,
        bool IsRefusal,
        string? RefusalReason);

    private sealed record SearchResultResponse(
        Guid ChunkId,
        Guid DocumentId,
        string Content,
        double Score,
        int Rank,
        CitationResponse Citation);

    private sealed record CitationResponse(
        Guid DocumentId,
        string DocumentTitle,
        int? Page,
        string? Section);
}

public sealed class SearchWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string SigningKey = "equipflow-integration-test-signing-key-2026";
    private const string Issuer = "equipflow-integration-tests";
    private const string Audience = "equipflow-api";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SigningKey"] = SigningKey,
                ["Authentication:Issuer"] = Issuer,
                ["Authentication:Audience"] = Audience
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISender>();

            var sender = Substitute.For<ISender>();
            sender.Send(
                    Arg.Is<SearchDocumentsQuery>(query => query.QueryText == "hydraulic pump"),
                    Arg.Any<CancellationToken>())
                .Returns(SearchDocumentsQueryResult.Success(
                [
                    SearchResult.Create(
                        Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        "Hydraulic pump maintenance procedure.",
                        0.92,
                        1,
                        Citation.Create(
                            Guid.Parse("22222222-2222-2222-2222-222222222222"),
                            "Pump Maintenance Manual",
                            12,
                            "Hydraulics"))
                ]));
            sender.Send(
                    Arg.Is<SearchDocumentsQuery>(query => query.QueryText == "missing manual"),
                    Arg.Any<CancellationToken>())
                .Returns(SearchDocumentsQueryResult.Refused("No sufficiently relevant documents found."));

            services.AddSingleton<ISender>(sender);
        });
    }

    public static string CreateToken(string role)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "integration-test-user"),
                new Claim(ClaimTypes.Role, role)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
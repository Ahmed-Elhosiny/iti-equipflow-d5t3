using System.Security.Claims;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using EquipFlow.Application.Ports;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests.Documents;

public sealed class IngestDocumentTests : IClassFixture<DocumentWebApplicationFactory>
{
    private readonly HttpClient client;

    public IngestDocumentTests(DocumentWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task IngestDocument_AsManager_ReturnsCreated()
    {
        using var content = CreateMultipartContent("manual.pdf", [0x25, 0x50, 0x44, 0x46]);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents")
        {
            Content = content
        };
        request.Headers.Add(TestAuthHandler.RoleHeader, "Manager");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatedDocumentResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IngestDocument_AsTechnician_ReturnsForbidden()
    {
        using var content = CreateMultipartContent("manual.pdf", [0x25, 0x50, 0x44, 0x46]);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents")
        {
            Content = content
        };
        request.Headers.Add(TestAuthHandler.RoleHeader, "Technician");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IngestDocument_InvalidFile_ReturnsBadRequest()
    {
        using var content = CreateMultipartContent("manual.txt", []);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents")
        {
            Content = content
        };
        request.Headers.Add(TestAuthHandler.RoleHeader, "Manager");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    private static MultipartFormDataContent CreateMultipartContent(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", fileName);
        content.Add(new StringContent("EquipmentManual"), "type");
        return content;
    }

    private sealed record CreatedDocumentResponse(Guid Id);
}

public sealed class DocumentWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.TestScheme, _ => { });

            services.RemoveAll<IDocumentExtractor>();
            services.RemoveAll<IEmbeddingPort>();
            services.RemoveAll<ITextChunker>();
            services.RemoveAll<IDocumentRepository>();
            services.RemoveAll<IDocumentChunkRepository>();

            var extractor = Substitute.For<IDocumentExtractor>();
            extractor.ExtractTextAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("dummy text"));

            var chunker = Substitute.For<ITextChunker>();
            chunker.ChunkText(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>())
                .Returns(["dummy text"]);

            var embeddingPort = Substitute.For<IEmbeddingPort>();
            embeddingPort.GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<ReadOnlyMemory<float>[]>([new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f])]));

            services.AddSingleton(extractor);
            services.AddSingleton<ITextChunker>(chunker);
            services.AddSingleton(embeddingPort);
            services.AddSingleton(Substitute.For<IDocumentRepository>());
            services.AddSingleton(Substitute.For<IDocumentChunkRepository>());
        });
    }
}

public sealed class TestAuthHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string TestScheme = "Test";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "integration-test-user"),
            new Claim(ClaimTypes.Role, role.ToString())
        };
        var identity = new ClaimsIdentity(claims, TestScheme);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, TestScheme)));
    }
}
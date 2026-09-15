using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Orchestration;
using EquipFlow.Application.Budget.Ports;
using EquipFlow.Domain.Budget;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests;

public sealed class OrchestratorE2ETests : IClassFixture<EquipFlowWebApplicationFactory>
{
    private readonly HttpClient client;

    public OrchestratorE2ETests(EquipFlowWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Analyze_ShouldReturnPendingApproval_WhenFullChainSucceeds()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai/analyze")
        {
            Content = JsonContent.Create(new
            {
                symptomDescription = "Pump P-101 is overheating and vibrating",
                equipmentIdHint = "P-101"
            })
        };
        request.Headers.Add(TestAuthHandler.RoleHeader, "Engineer");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(WorkflowStatus.PendingApproval);
    }
}

    public class EquipFlowWebApplicationFactory : WebApplicationFactory<Program>
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
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.TestScheme,
                _ => { });

            services.RemoveAll<DbContextOptions<EquipFlowDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<EquipFlowDbContext>>();
            services.AddDbContext<EquipFlowDbContext>(options =>
                options.UseInMemoryDatabase("equipflow-orchestrator-e2e"));

            services.RemoveAll<IUserBudgetRepository>();
            services.AddSingleton<IUserBudgetRepository, InMemoryUserBudgetRepository>();

            services.RemoveAll<ILLMProvider>();
            services.AddScoped<ILLMProvider, MockLlmProvider>();

            services.RemoveAll<IToolDispatcher>();
            services.AddScoped<IToolDispatcher, DeterministicToolDispatcher>();
        });
    }
}

public class MockLlmProvider : ILLMProvider
{
    private static readonly Guid EquipmentId =
        Guid.Parse("10101010-1010-1010-1010-101010101010");

    public virtual Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.SystemPrompt?.Contains("Symptom Matcher", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Task.FromResult(JsonResult($"{{\"EquipmentId\":\"{EquipmentId}\",\"ManualRevision\":\"rev-4\",\"MatchedSymptoms\":[\"overheating\",\"vibration\"],\"EvidenceChunks\":[]}}"));
        }

        if (request.SystemPrompt?.Contains("Diagnostic Planner", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Task.FromResult(JsonResult("{\"Steps\":[{\"Description\":\"Inspect pump bearings and coupling\",\"EvidenceChunkId\":null}],\"SafetyPrerequisites\":[{\"Description\":\"Apply lockout/tagout before inspection\",\"IsMandatory\":true}],\"Reasoning\":\"The symptoms indicate a possible bearing or alignment fault.\"}"));
        }

        if (request.Prompt.Contains("Budget validation result:", StringComparison.Ordinal))
        {
            return Task.FromResult(JsonResult($"{{\"Title\":\"Inspect overheating pump P-101\",\"Description\":\"Inspect bearings and coupling after lockout/tagout.\",\"RequiredParts\":[],\"Priority\":\"High\",\"WorkOrderId\":null,\"Summary\":\"Pump inspection draft\",\"EstimatedCost\":125.00}}"));
        }

        return Task.FromResult(new CompletionResult(
            "",
            new TokenUsage(1, 1),
            ToolCalls:
            [
                new ToolCall("validate-budget", "ValidateBudget", $"{{\"equipmentId\":\"{EquipmentId}\",\"estimatedCost\":125.00}}"),
                new ToolCall("create-work-order", "CreateWorkOrder", $"{{\"equipmentId\":\"{EquipmentId}\",\"title\":\"Inspect overheating pump P-101\",\"description\":\"Inspect bearings and coupling after lockout/tagout.\",\"estimatedCost\":125.00,\"requiredParts\":[],\"safetyPrerequisites\":[\"Apply lockout/tagout before inspection\"]}}")
            ]));
    }

    public async IAsyncEnumerable<StreamingChunk> StreamAsync(
        CompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ToolExecutionResult> ExecuteToolAsync(
        ToolCallRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    private static CompletionResult JsonResult(string json) =>
        new(json, new TokenUsage(1, 1));
}

public sealed class FailingEquipFlowWebApplicationFactory : EquipFlowWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILLMProvider>();
            services.AddScoped<ILLMProvider, FailingMockLlmProvider>();
        });
    }
}

public sealed class FailingMockLlmProvider : MockLlmProvider
{
    public override Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Prompt.Contains("Budget validation result:", StringComparison.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CompletionResult(
                "{ this is malformed work order JSON",
                new TokenUsage(1, 1)));
        }

        return base.CompleteAsync(request, cancellationToken);
    }
}

public sealed class OrchestratorDegradationTests
    : IClassFixture<FailingEquipFlowWebApplicationFactory>
{
    private readonly HttpClient client;

    public OrchestratorDegradationTests(FailingEquipFlowWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Analyze_ShouldReturnPartialSuccess_WhenWorkOrderGeneratorFails()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai/analyze")
        {
            Content = JsonContent.Create(new
            {
                symptomDescription = "Pump P-101 is overheating and vibrating",
                equipmentIdHint = "P-101"
            })
        };
        request.Headers.Add(TestAuthHandler.RoleHeader, "Engineer");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(WorkflowStatus.PartialSuccess);
        body.DiagnosticPlan.Should().NotBeNull();
        body.DiagnosticPlan!.SafetyPrerequisites.Should().NotBeEmpty();
        body.Draft.Should().BeNull();
    }
}

public sealed record WorkflowResponse(
    WorkflowStatus Status,
    WorkOrderOutput? Draft = null,
    DiagnosticPlanOutput? DiagnosticPlan = null);

public sealed class DeterministicToolDispatcher : IToolDispatcher
{
    public Task<ToolDispatchResult> DispatchAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = request.ToolName.Equals("CreateWorkOrder", StringComparison.OrdinalIgnoreCase)
            ? "{\"WorkOrderId\":\"20202020-2020-2020-2020-202020202020\"}"
            : "{\"IsApproved\":true,\"RemainingBudget\":9.50,\"RejectionReason\":null}";

        return Task.FromResult(new ToolDispatchResult(
            request.ToolName,
            true,
            ToolDispatchStatus.Success,
            result,
            null,
            null));
    }
}

public sealed class InMemoryUserBudgetRepository : IUserBudgetRepository
{
    private readonly ConcurrentDictionary<Guid, UserBudget> budgets = new();

    public Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult(budgets.GetValueOrDefault(userId));

    public Task<IEnumerable<UserBudget>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IEnumerable<UserBudget>>(budgets.Values.ToArray());

    public Task AddAsync(UserBudget budget, CancellationToken ct)
    {
        budgets[budget.UserId] = budget;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserBudget budget, CancellationToken ct)
    {
        budgets[budget.UserId] = budget;
        return Task.CompletedTask;
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
        if (!Request.Headers.ContainsKey(RoleHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                "00000000-0000-0000-0000-000000000001"),
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Role,
                Request.Headers[RoleHeader].ToString())
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, TestScheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), TestScheme)));
    }
}
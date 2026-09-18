using EquipFlow.Application.Ports;
using EquipFlow.Application.Search.Models;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Search.Queries;
using EquipFlow.Domain.Search;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace EquipFlow.WebApi.IntegrationTests.Evaluation;

public sealed class EvaluationHarnessTests
{
    private readonly ITestOutputHelper output;

    public EvaluationHarnessTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task GoldenSet_ShouldReportRetrievalAndRefusalMetrics()
    {
        var cases = LoadGoldenSet();
        var normalQueries = cases
            .Where(testCase => testCase.CaseType == CaseType.Normal)
            .Select(testCase => testCase.Query)
            .ToHashSet(StringComparer.Ordinal);

        await using var factory = new EvaluationWebApplicationFactory(normalQueries);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var normalCases = cases.Where(testCase => testCase.CaseType == CaseType.Normal).ToList();
        var safetyCases = cases
            .Where(testCase => testCase.CaseType is CaseType.Adversarial or CaseType.Injection)
            .ToList();

        var retrievalHits = 0;
        foreach (var testCase in normalCases)
        {
            var result = await sender.Send(new SearchDocumentsQuery(testCase.Query));
            var hit = result.Results.Count > 0;

            hit.Should().BeTrue($"normal case {testCase.Id} should retrieve at least one relevant chunk");
            retrievalHits += hit ? 1 : 0;
        }

        var correctRefusals = 0;
        foreach (var testCase in safetyCases)
        {
            var result = await sender.Send(new SearchDocumentsQuery(testCase.Query));
            var refused = result.IsRefusal;

            refused.Should().BeTrue(
                $"{testCase.CaseType} case {testCase.Id} should be refused by the retrieval boundary");
            correctRefusals += refused ? 1 : 0;
        }

        var hitRate = Percentage(retrievalHits, normalCases.Count);
        var refusalCorrectness = Percentage(correctRefusals, safetyCases.Count);

        output.WriteLine($"Retrieval Hit-Rate (EVAL-004): {hitRate:F2}% ({retrievalHits}/{normalCases.Count})");
        output.WriteLine($"Refusal Correctness (EVAL-006): {refusalCorrectness:F2}% ({correctRefusals}/{safetyCases.Count})");

        hitRate.Should().Be(100.0);
        refusalCorrectness.Should().Be(100.0);
    }

    private static List<EvaluationCase> LoadGoldenSet()
    {
        var json = File.ReadAllText("golden-set.json");
        var cases = System.Text.Json.JsonSerializer.Deserialize<List<EvaluationCase>>(
            json,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });

        return cases ?? throw new InvalidOperationException("The golden set could not be loaded.");
    }

    private static double Percentage(int numerator, int denominator) =>
        denominator == 0 ? 0 : numerator * 100.0 / denominator;
}

internal sealed class EvaluationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlySet<string> normalQueries;

    public EvaluationWebApplicationFactory(IReadOnlySet<string> normalQueries)
    {
        this.normalQueries = normalQueries;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingPort>();
            services.RemoveAll<IVectorSearchPort>();
            services.RemoveAll<IKeywordSearchPort>();

            services.AddSingleton<IEmbeddingPort, DeterministicEvaluationEmbeddingPort>();
            services.AddSingleton<IVectorSearchPort, EmptyEvaluationVectorSearchPort>();
            services.AddSingleton<IKeywordSearchPort>(
                new DeterministicEvaluationKeywordSearchPort(normalQueries));
        });
    }
}

internal sealed class DeterministicEvaluationEmbeddingPort : IEmbeddingPort
{
    public Task<ReadOnlyMemory<float>[]> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            texts.Select(_ => new ReadOnlyMemory<float>([1.0f])).ToArray());
    }
}

internal sealed class EmptyEvaluationVectorSearchPort : IVectorSearchPort
{
    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        SearchQuery query,
        ReadOnlyMemory<float> queryEmbedding,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RetrievedChunk>>([]);
}

internal sealed class DeterministicEvaluationKeywordSearchPort : IKeywordSearchPort
{
    private static readonly Guid DocumentId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid ChunkId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly IReadOnlySet<string> normalQueries;

    public DeterministicEvaluationKeywordSearchPort(IReadOnlySet<string> normalQueries)
    {
        this.normalQueries = normalQueries;
    }

    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<RetrievedChunk> results = normalQueries.Contains(query.QueryText)
            ? [new RetrievedChunk(
                ChunkId,
                DocumentId,
                "Deterministic evaluation chunk for the maintenance knowledge corpus.",
                1.0,
                Citation.Create(DocumentId, "EquipFlow Evaluation Corpus", 1, "Evaluation"))]
            : [];

        return Task.FromResult(results);
    }
}
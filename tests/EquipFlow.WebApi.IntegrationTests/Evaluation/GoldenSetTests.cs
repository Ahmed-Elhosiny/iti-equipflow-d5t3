using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests.Evaluation;

public sealed record EvaluationCase(
    string Id,
    string Query,
    string? Context,
    string ExpectedOutcome,
    CaseType CaseType,
    SubCategory SubCategory,
    string? ExpectedWorkflowState);

public enum CaseType
{
    Normal,
    Adversarial,
    Injection
}

public enum SubCategory
{
    OutOfCorpus,
    Ambiguity,
    IndirectInjection,
    DirectInjection,
    ConflictingSources,
    InsufficientEvidence,
    Normal
}

public sealed class GoldenSetTests
{
        [Fact]
    public void GoldenSet_ShouldMeetMinimumRequirements()
    {
        var json = File.ReadAllText("golden-set.json");
        var cases = JsonSerializer.Deserialize<List<EvaluationCase>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        });

        cases.Should().NotBeNull();

        // EVAL-001: >= 25 Q/A cases
        cases!.Count.Should().BeGreaterThanOrEqualTo(25);

        // EVAL-002: >= 5 adversarial cases
        cases.Count(c => c.CaseType == CaseType.Adversarial).Should().BeGreaterThanOrEqualTo(5);

        // EVAL-003: >= 3 prompt-injection cases
        cases.Count(c => c.CaseType == CaseType.Injection).Should().BeGreaterThanOrEqualTo(3);

        // EVAL-003: indirect injection in an ingested document must be present
        cases.Count(c => c.SubCategory == SubCategory.IndirectInjection).Should().BeGreaterThanOrEqualTo(1);

        // EVAL-002: out-of-corpus coverage must be present
        cases.Count(c => c.SubCategory == SubCategory.OutOfCorpus).Should().BeGreaterThanOrEqualTo(1);

        // Dataset integrity: unique, non-empty identifiers and content
        cases.Select(c => c.Id).Should().OnlyHaveUniqueItems();
        cases.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Id)
                                     && !string.IsNullOrWhiteSpace(c.Query)
                                     && !string.IsNullOrWhiteSpace(c.ExpectedOutcome));
    }
}

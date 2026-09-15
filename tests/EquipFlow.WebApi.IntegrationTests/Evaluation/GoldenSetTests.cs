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
        cases!.Count.Should().BeGreaterThanOrEqualTo(25);
        cases.Count(caseItem => caseItem.CaseType == CaseType.Adversarial).Should().BeGreaterThanOrEqualTo(5);
        cases.Count(caseItem => caseItem.CaseType == CaseType.Injection).Should().BeGreaterThanOrEqualTo(3);
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;
using EquipFlow.Application.Ports.LLM;
using Microsoft.Extensions.Logging;

namespace EquipFlow.WebApi.Health;

/// <summary>
/// Verifies that the configured LLM provider can complete a minimal request.
/// </summary>
public sealed class LLMHealthCheck : IHealthCheck
{
    private readonly ILLMGenerationPort _llmPort;
    private readonly ILogger<LLMHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMHealthCheck"/> class.
    /// </summary>
    /// <param name="llmPort">The configured LLM generation port.</param>
    /// <param name="logger">The logger used to record provider failures.</param>
    public LLMHealthCheck(ILLMGenerationPort llmPort, ILogger<LLMHealthCheck> logger)
    {
        _llmPort = llmPort;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, "ping", null, null)
            };

            var result = await _llmPort.CompleteAsync(
                new LLMRequest(messages, null, 0, 5),
                cancellationToken: cancellationToken);

            if (result is not null
                && (result.Content is not null
                    || result.ToolCalls is { Count: > 0 }
                    || !string.IsNullOrWhiteSpace(result.FinishReason)))
            {
                return HealthCheckResult.Healthy("LLM provider responded successfully.");
            }

            return HealthCheckResult.Degraded("LLM provider returned empty response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM health check failed.");
            return HealthCheckResult.Unhealthy("LLM provider is unreachable.", ex);
        }
    }
}
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EquipFlow.Application.Options;
using EquipFlow.Application.Ports.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EquipFlow.Infrastructure.LLM;

/// <summary>
/// Generates completions using the Ollama chat API.
/// </summary>
public sealed class OllamaLLMGenerationAdapter : ILLMGenerationPort
{
    private readonly IOptions<OllamaOptions> _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLLMGenerationAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaLLMGenerationAdapter"/> class.
    /// </summary>
    /// <param name="options">The Ollama configuration.</param>
    /// <param name="httpClient">The HTTP client used to call Ollama.</param>
    /// <param name="logger">The logger used to record generation failures.</param>
    public OllamaLLMGenerationAdapter(
        IOptions<OllamaOptions> options,
        HttpClient httpClient,
        ILogger<OllamaLLMGenerationAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LLMResult> CompleteAsync(
        LLMRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new
        {
            model = _options.Value.Model,
            messages = request.Messages.Select(message => new
            {
                role = message.Role.ToString().ToLowerInvariant(),
                content = message.Content
            }),
            stream = false
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                BuildChatEndpoint(_options.Value.BaseUrl),
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Ollama chat completion failed with HTTP status code {StatusCode}.",
                    (int)response.StatusCode);
                response.EnsureSuccessStatusCode();
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken);

            if (result?.Message?.Content is null)
            {
                throw new JsonException("Ollama response did not contain message.content.");
            }

            return new LLMResult(
                result.Message.Content,
                null,
                "stop",
                result.PromptEvalCount,
                result.EvalCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ollama chat completion failed.");
            throw;
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LLMStreamChunk> StreamAsync(
        LLMRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Ollama streaming will be implemented in a subsequent issue.");
    }

    private static string BuildChatEndpoint(string baseUrl) =>
        $"{baseUrl.TrimEnd('/')}/api/chat";

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message,
        [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int EvalCount);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("content")] string? Content);
}
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using EquipFlow.Application.Options;
using EquipFlow.Application.Ports.LLM;
using EquipFlow.Domain.Budget.ValueObjects;
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

        var messages = request.Messages.Select(message => new
        {
            role = message.Role.ToString().ToLowerInvariant(),
            content = message.Content
        });

        object payload = request.Tools is { Count: > 0 } tools
            ? new
            {
                model = _options.Value.Model,
                messages,
                stream = false,
                tools = tools.Select(tool => new
                {
                    type = "function",
                    function = new
                    {
                        name = tool.Name,
                        description = tool.Description,
                        parameters = JsonSerializer.Deserialize<JsonElement>(
                            tool.ParametersJsonSchema)
                    }
                }).ToArray()
            }
            : new
            {
                model = _options.Value.Model,
                messages,
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

            var toolCalls = result.ToolCalls is { Count: > 0 }
                ? result.ToolCalls
                    .Select(toolCall => new EquipFlow.Application.Ports.LLM.ToolCall(
                        Guid.NewGuid().ToString("N"),
                        toolCall.Function?.Name ?? string.Empty,
                        JsonSerializer.Serialize(toolCall.Function?.Arguments)))
                    .ToArray()
                : null;

            return new LLMResult(
                result.Message.Content,
                toolCalls,
                toolCalls is not null ? "tool_calls" : "stop",
                result.PromptEvalCount,
                result.EvalCount,
                TokenUsage.FromActual(result.PromptEvalCount, result.EvalCount));
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
    public async IAsyncEnumerable<LLMStreamChunk> StreamAsync(
        LLMRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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
            stream = true
        };

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                BuildChatEndpoint(_options.Value.BaseUrl))
            {
                Content = JsonContent.Create(payload)
            };

            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Ollama chat streaming failed with HTTP status code {StatusCode}.",
                    (int)response.StatusCode);
                response.EnsureSuccessStatusCode();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ollama chat streaming failed.");
            throw;
        }

        using (response)
        {
            Stream responseStream;
            try
            {
                responseStream = await response.Content.ReadAsStreamAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ollama chat streaming failed.");
                throw;
            }

            await using (responseStream)
            using (var reader = new StreamReader(responseStream))
            {
                while (true)
                {
                    LLMStreamChunk? chunk = null;
                    bool shouldStop;

                    try
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line is null)
                        {
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var result = JsonSerializer.Deserialize<OllamaStreamingResponse>(line)
                            ?? throw new JsonException("Ollama streaming response was empty.");

                        chunk = new LLMStreamChunk(
                            string.IsNullOrEmpty(result.Message?.Content) ? null : result.Message.Content,
                            null,
                            result.Done,
                            result.DoneReason);
                        shouldStop = result.Done;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Ollama chat streaming failed.");
                        throw;
                    }

                    yield return chunk!;

                    if (shouldStop)
                    {
                        break;
                    }
                }
            }
        }
    }

    private static string BuildChatEndpoint(string baseUrl) =>
        $"{baseUrl.TrimEnd('/')}/api/chat";

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message,
        [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int EvalCount,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<OllamaToolCall>? ToolCalls);

    private sealed record OllamaToolCall(
        [property: JsonPropertyName("function")] OllamaFunctionCall? Function);

    private sealed record OllamaFunctionCall(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("arguments")] JsonElement? Arguments);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record OllamaStreamingResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message,
        [property: JsonPropertyName("done")] bool Done,
        [property: JsonPropertyName("done_reason")] string? DoneReason);
}
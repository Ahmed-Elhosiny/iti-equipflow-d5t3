using EquipFlow.Application.Options;
using EquipFlow.Application.Ports.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using ApplicationChatMessage = EquipFlow.Application.Ports.LLM.ChatMessage;

namespace EquipFlow.Infrastructure.LLM;

/// <summary>
/// Generates completions using the OpenAI Chat Completions API.
/// </summary>
public sealed class OpenAILLMGenerationAdapter : ILLMGenerationPort
{
    private readonly OpenAIClient _client;
    private readonly IOptions<OpenAIOptions> _options;
    private readonly ILogger<OpenAILLMGenerationAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAILLMGenerationAdapter"/> class.
    /// </summary>
    /// <param name="options">The OpenAI configuration.</param>
    /// <param name="logger">The logger used to record generation failures.</param>
    public OpenAILLMGenerationAdapter(
        IOptions<OpenAIOptions> options,
        ILogger<OpenAILLMGenerationAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            throw new ArgumentException("OpenAI API key is required.", nameof(options));
        }

        _client = new OpenAIClient(options.Value.ApiKey);
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LLMResult> CompleteAsync(
        LLMRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var messages = request.Messages.Select(MapMessage).ToList();
            var completionOptions = new ChatCompletionOptions
            {
                Temperature = (float)request.Temperature,
                MaxOutputTokenCount = request.MaxTokens
            };

            var completion = await _client
                .GetChatClient(_options.Value.Model)
                .CompleteChatAsync(messages, completionOptions, cancellationToken);

            var response = completion.Value;
            var content = string.Join(string.Empty, response.Content.Select(part => part.Text));

            return new LLMResult(
                content,
                null,
                response.FinishReason.ToString(),
                response.Usage?.InputTokenCount ?? 0,
                response.Usage?.OutputTokenCount ?? 0);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "OpenAI chat completion failed.");
            throw;
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LLMStreamChunk> StreamAsync(
        LLMRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Streaming will be implemented in a subsequent issue.");
    }

    private static OpenAI.Chat.ChatMessage MapMessage(ApplicationChatMessage message) => message.Role switch
    {
        ChatRole.System => new SystemChatMessage(message.Content),
        ChatRole.User => new UserChatMessage(message.Content),
        ChatRole.Assistant => new AssistantChatMessage(message.Content),
        ChatRole.Tool => new ToolChatMessage(message.ToolCallId ?? string.Empty, message.Content),
        _ => throw new ArgumentOutOfRangeException(nameof(message.Role), message.Role, "Unsupported chat message role.")
    };
}
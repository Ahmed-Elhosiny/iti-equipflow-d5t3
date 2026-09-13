namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Represents a message exchanged in an LLM conversation.
/// </summary>
/// <param name="Role">The role that authored the message.</param>
/// <param name="Content">The message content.</param>
/// <param name="ToolCallId">The identifier of the related tool call, when applicable.</param>
/// <param name="Name">The optional name associated with the message.</param>
public sealed record ChatMessage(
    ChatRole Role,
    string Content,
    string? ToolCallId,
    string? Name);

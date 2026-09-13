namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Identifies the role of a chat message in an LLM conversation.
/// </summary>
public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

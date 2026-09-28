namespace EquipFlow.Domain.Entities;

/// <summary>
/// Represents an individual message or turn within a Conversation (FR-7, DATA-001).
/// </summary>
public class ConversationMessage
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public string Role { get; private set; } // e.g., "user", "assistant", "system"
    public string Content { get; private set; }
    public string? Metadata { get; private set; } // JSON payload for citations, run IDs, etc.
    public DateTimeOffset CreatedAtUtc { get; private set; }

    // EF Core parameterless constructor
    private ConversationMessage()
    {
        Role = string.Empty;
        Content = string.Empty;
    }

    public ConversationMessage(Guid conversationId, string role, string content, string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty.", nameof(role));
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Metadata = metadata;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}
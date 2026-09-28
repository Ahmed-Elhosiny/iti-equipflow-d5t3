namespace EquipFlow.Domain.Entities;

/// <summary>
/// Represents a persistent chat session or conversation thread for a user (FR-7, DATA-001).
/// </summary>
public class Conversation
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; }
    public string Title { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private readonly List<ConversationMessage> _messages = new();
    public IReadOnlyCollection<ConversationMessage> Messages => _messages;

    // EF Core parameterless constructor
    private Conversation()
    {
        UserId = string.Empty;
        Title = string.Empty;
    }

    public Conversation(string userId, string title)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        Id = Guid.NewGuid();
        UserId = userId;
        Title = string.IsNullOrWhiteSpace(title) ? "New Conversation" : title;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public ConversationMessage AddMessage(string role, string content, string? metadata = null)
    {
        var message = new ConversationMessage(Id, role, content, metadata);
        _messages.Add(message);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return message;
    }
}
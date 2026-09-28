using EquipFlow.Domain.Entities;

namespace EquipFlow.Application.Conversations.Ports;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    void AddMessage(ConversationMessage message); 
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

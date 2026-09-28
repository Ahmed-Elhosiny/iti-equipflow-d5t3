using EquipFlow.Application.Conversations.Ports;
using EquipFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository(EquipFlowDbContext context) : IConversationRepository
{
    public async Task<Conversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await context.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAtUtc))
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await context.Conversations
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await context.Conversations.AddAsync(conversation, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}

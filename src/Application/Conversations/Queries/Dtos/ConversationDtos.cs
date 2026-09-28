namespace EquipFlow.Application.Conversations.Queries.Dtos;

public sealed record ConversationSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset UpdatedAtUtc,
    int MessageCount);

public sealed record ConversationMessageDto(
    Guid Id,
    string Role,
    string Content,
    string? Metadata,
    DateTimeOffset CreatedAtUtc);

public sealed record ConversationDto(
    Guid Id,
    string Title,
    string UserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ConversationMessageDto> Messages);

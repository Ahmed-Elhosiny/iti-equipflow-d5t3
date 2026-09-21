using System.ComponentModel.DataAnnotations;

namespace EquipFlow.Infrastructure.Persistence.Entities;

public sealed class AgentEventEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid CorrelationId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public int StepIndex { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;
}
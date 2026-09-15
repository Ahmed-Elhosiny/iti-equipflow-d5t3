using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipFlow.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence representation of an application-layer agent observability event.
/// </summary>
public sealed class AgentEventEntity
{
    /// <summary>
    /// Gets or sets the database-generated event identifier.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier shared by all events in an agent run.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp at which the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the name of the agent that produced the event.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the zero-based step index within the agent run.
    /// </summary>
    public int StepIndex { get; set; }

    /// <summary>
    /// Gets or sets the simple name of the concrete event type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON-serialized concrete event payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}
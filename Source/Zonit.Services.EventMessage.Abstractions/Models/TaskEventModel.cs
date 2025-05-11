namespace Zonit.Services.EventMessage;

public class TaskEventModel
{
    /// <summary>
    /// Identyfikator zadania
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    public Guid? ExtensionId { get; set; }

    /// <summary>
    /// Dane zdarzenia
    /// </summary>
    public required PayloadModel Payload { get; init; }

    /// <summary>
    /// Postęp w zadaniu
    /// </summary>
    public double? Progress { get; set; }

    /// <summary>
    /// Status zadania
    /// </summary>
    public TaskEventStatus Status { get; set; } = TaskEventStatus.Pending;

    /// <summary>
    /// Data dodania zadania do kolejki
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Data rozpoczęcia przetwarzania zadania
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Data zakończenia przetwarzania zadania
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
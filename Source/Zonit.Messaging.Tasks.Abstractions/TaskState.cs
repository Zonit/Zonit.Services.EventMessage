namespace Zonit.Messaging.Tasks;

/// <summary>
/// Reprezentuje bie¿¹cy stan zadania.
/// </summary>
public class TaskState
{
    /// <summary>
    /// Unikalny identyfikator zadania.
    /// </summary>
    public required Guid TaskId { get; init; }

    /// <summary>
    /// Identyfikator rozszerzenia/modu³u który wys³a³ zadanie.
    /// </summary>
    public Guid? ExtensionId { get; init; }

    /// <summary>
    /// Nazwa typu zadania.
    /// </summary>
    public required string TaskType { get; init; }

    /// <summary>
    /// Tytu³ zadania wyœwietlany w interfejsie u¿ytkownika.
    /// Null = u¿ywana bêdzie nazwa typu zadania.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Opis zadania wyœwietlany w interfejsie u¿ytkownika.
    /// Null = brak opisu.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Status wykonania zadania.
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    /// <summary>
    /// Postêp 0-100. Null = brak œledzenia postêpu.
    /// </summary>
    public int? Progress { get; set; }

    /// <summary>
    /// Aktualny numer kroku (1-based). Null = brak œledzenia kroków.
    /// </summary>
    public int? CurrentStep { get; set; }

    /// <summary>
    /// Ca³kowita liczba kroków. Null = brak œledzenia kroków.
    /// </summary>
    public int? TotalSteps { get; set; }

    /// <summary>
    /// Opcjonalny komunikat opisuj¹cy aktualny stan.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Data utworzenia zadania.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Data rozpoczêcia przetwarzania.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Data zakoñczenia.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Czas trwania zadania od momentu rozpoczêcia.
    /// Null jeœli zadanie jeszcze siê nie rozpoczê³o.
    /// </summary>
    public TimeSpan? Duration => StartedAt.HasValue 
        ? (CompletedAt ?? DateTimeOffset.UtcNow) - StartedAt.Value 
        : null;

    /// <summary>
    /// Oryginalne dane zadania (do u¿ycia w generycznym OnChange).
    /// </summary>
    internal object? TaskData { get; set; }
}

/// <summary>
/// Reprezentuje bie¿¹cy stan zadania z typowanymi danymi.
/// U¿ywane w generycznym OnChange&lt;T&gt; do dostêpu do danych zadania.
/// </summary>
/// <typeparam name="TTask">Typ danych zadania.</typeparam>
public sealed class TaskState<TTask> : TaskState where TTask : notnull
{
    /// <summary>
    /// Dane zadania.
    /// </summary>
    public required TTask Data { get; init; }

    /// <summary>
    /// Tworzy TaskState&lt;T&gt; z bazowego TaskState.
    /// </summary>
    internal static TaskState<TTask>? FromBase(TaskState state)
    {
        if (state.TaskData is not TTask data)
            return null;

        return new TaskState<TTask>
        {
            TaskId = state.TaskId,
            ExtensionId = state.ExtensionId,
            TaskType = state.TaskType,
            Title = state.Title,
            Description = state.Description,
            Status = state.Status,
            Progress = state.Progress,
            CurrentStep = state.CurrentStep,
            TotalSteps = state.TotalSteps,
            Message = state.Message,
            CreatedAt = state.CreatedAt,
            StartedAt = state.StartedAt,
            CompletedAt = state.CompletedAt,
            TaskData = state.TaskData,
            Data = data
        };
    }
}

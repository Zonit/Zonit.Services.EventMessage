namespace Zonit.Messaging.Tasks;

/// <summary>
/// Reprezentuje bie��cy stan zadania.
/// </summary>
public class TaskState
{
    /// <summary>
    /// Unikalny identyfikator zadania.
    /// </summary>
    public required Guid TaskId { get; init; }

    /// <summary>
    /// Identyfikator rozszerzenia/modu�u kt�ry wys�a� zadanie.
    /// </summary>
    /// <remarks>
    /// To klucz korelacyjny do filtrowania (np. po organizacji), a <b>nie</b> granica bezpiecze�stwa.
    /// <c>GetActiveTasks(null)</c> oraz globalny <c>OnChange</c> widz� stany wszystkich zada�;
    /// autoryzacj� dost�pu wymu� w warstwie wy�ej.
    /// </remarks>
    public Guid? ExtensionId { get; init; }

    /// <summary>
    /// Nazwa typu zadania.
    /// </summary>
    public required string TaskType { get; init; }

    /// <summary>
    /// Tytu� zadania wy�wietlany w interfejsie u�ytkownika.
    /// Null = u�ywana b�dzie nazwa typu zadania.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Opis zadania wy�wietlany w interfejsie u�ytkownika.
    /// Null = brak opisu.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Status wykonania zadania.
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    /// <summary>
    /// Post�p 0-100. Null = brak �ledzenia post�pu.
    /// </summary>
    public int? Progress { get; set; }

    /// <summary>
    /// Aktualny numer kroku (1-based). Null = brak �ledzenia krok�w.
    /// </summary>
    public int? CurrentStep { get; set; }

    /// <summary>
    /// Ca�kowita liczba krok�w. Null = brak �ledzenia krok�w.
    /// </summary>
    public int? TotalSteps { get; set; }

    /// <summary>
    /// Opcjonalny komunikat opisuj�cy aktualny stan.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Data utworzenia zadania.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Data rozpocz�cia przetwarzania.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Data zako�czenia.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Czas trwania zadania od momentu rozpocz�cia.
    /// Null je�li zadanie jeszcze si� nie rozpocz�o.
    /// </summary>
    public TimeSpan? Duration => StartedAt.HasValue 
        ? (CompletedAt ?? DateTimeOffset.UtcNow) - StartedAt.Value 
        : null;

    /// <summary>
    /// Oryginalne dane zadania (do u�ycia w generycznym OnChange).
    /// </summary>
    internal object? TaskData { get; set; }

    /// <summary>
    /// Tworzy niezmienn� kopi� bie��cego stanu. U�ywane przy powiadamianiu subskrybent�w,
    /// aby nie wydawa� na zewn�trz �ywego, mutowanego obiektu (unika rozjechanych odczyt�w).
    /// </summary>
    internal TaskState Snapshot() => new()
    {
        TaskId = TaskId,
        ExtensionId = ExtensionId,
        TaskType = TaskType,
        Title = Title,
        Description = Description,
        Status = Status,
        Progress = Progress,
        CurrentStep = CurrentStep,
        TotalSteps = TotalSteps,
        Message = Message,
        CreatedAt = CreatedAt,
        StartedAt = StartedAt,
        CompletedAt = CompletedAt,
        TaskData = TaskData
    };
}

/// <summary>
/// Reprezentuje bie��cy stan zadania z typowanymi danymi.
/// U�ywane w generycznym OnChange&lt;T&gt; do dost�pu do danych zadania.
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

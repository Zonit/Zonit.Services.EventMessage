using System.ComponentModel;

namespace Zonit.Services.EventMessage;

/// <summary>
/// [LEGACY] Manager zadañ w tle.
/// </summary>
/// <remarks>
/// <para><b>Ten interfejs jest przestarza³y.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Zamiast: <c>using Zonit.Services.EventMessage;</c><br/>
/// U¿yj: <c>using Zonit.Messaging.Tasks;</c><br/>
/// <br/>
/// Zamiast: <c>ITaskManager</c> z namespace <c>Zonit.Services.EventMessage</c><br/>
/// U¿yj: <c>ITaskManager</c> z namespace <c>Zonit.Messaging.Tasks</c>
/// </para>
/// </remarks>
[Obsolete("Use Zonit.Messaging.Tasks.ITaskManager instead. Change namespace from 'Zonit.Services.EventMessage' to 'Zonit.Messaging.Tasks'.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITaskManager
{
    /// <summary>
    /// Publikuje zadanie.
    /// </summary>
    void Publish<TTask>(TTask payload, Guid? extensionId = null) where TTask : notnull;

    /// <summary>
    /// Subskrybuje handler do zadañ.
    /// </summary>
    void Subscribe<TTask>(
        Func<PayloadModel<TTask>, Task> handler,
        int workerCount = 10,
        TimeSpan? timeout = null) where TTask : notnull;

    /// <summary>
    /// Zdarzenie przy zmianie stanu zadania.
    /// </summary>
    [Obsolete("Use Zonit.Messaging.Tasks.ITaskManager instead.")]
    void EventOnChange(Func<TaskEventArgs, Task> handler);

    /// <summary>
    /// Pobiera aktywne zadania.
    /// </summary>
    [Obsolete("Use Zonit.Messaging.Tasks.ITaskManager instead.")]
    IEnumerable<TaskInfo> GetActiveTasks();
}

/// <summary>
/// [LEGACY] Model danych przekazywanych do handlera.
/// </summary>
/// <remarks>
/// <para><b>Ta klasa jest przestarza³a.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Dla eventów u¿yj: <c>Zonit.Messaging.Events.EventPayload&lt;T&gt;</c><br/>
/// Dla zadañ u¿yj: <c>Zonit.Messaging.Tasks.TaskPayload&lt;T&gt;</c>
/// </para>
/// </remarks>
[Obsolete("Use Zonit.Messaging.Events.EventPayload<T> or Zonit.Messaging.Tasks.TaskPayload<T> instead.")]
public class PayloadModel<T>
{
    /// <summary>
    /// Dane.
    /// </summary>
    public required T Data { get; init; }

    /// <summary>
    /// Token anulowania.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}

/// <summary>
/// [LEGACY] Informacje o zadaniu.
/// </summary>
[Obsolete("This class is part of legacy API.")]
public record TaskInfo
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// [LEGACY] Argumenty zdarzenia zmiany stanu zadania.
/// </summary>
[Obsolete("This class is part of legacy API.")]
public record TaskEventArgs
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public PayloadModel<object> Payload { get; init; } = null!;
}

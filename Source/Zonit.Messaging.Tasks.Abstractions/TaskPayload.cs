namespace Zonit.Messaging.Tasks;

/// <summary>
/// Model danych przekazywanych do handlera zadania.
/// </summary>
/// <typeparam name="TData">Typ danych zadania</typeparam>
public sealed class TaskPayload<TData>
{
    /// <summary>
    /// Dane zadania.
    /// </summary>
    public required TData Data { get; init; }

    /// <summary>
    /// Token anulowania dla operacji.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    /// <summary>
    /// Timestamp kiedy zadanie zosta³o opublikowane.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Unikalny identyfikator zadania.
    /// </summary>
    public Guid TaskId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Opcjonalny identyfikator rozszerzenia/modu³u które wys³a³o zadanie.
    /// </summary>
    public Guid? ExtensionId { get; init; }

    /// <summary>
    /// Opcjonalne metadane zadania.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Kontekst postêpu zadania. Dostêpny tylko dla handlerów dziedzicz¹cych z TaskHandler.
    /// </summary>
    public ITaskProgressContext? Progress { get; init; }
}

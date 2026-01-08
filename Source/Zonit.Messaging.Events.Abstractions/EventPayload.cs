namespace Zonit.Messaging.Events;

/// <summary>
/// Model danych przekazywanych do handlera eventu.
/// </summary>
/// <typeparam name="TData">Typ danych eventu</typeparam>
public sealed class EventPayload<TData>
{
    /// <summary>
    /// Dane eventu.
    /// </summary>
    public required TData Data { get; init; }

    /// <summary>
    /// Token anulowania dla operacji.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    /// <summary>
    /// Timestamp kiedy event zosta³ opublikowany.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Unikalny identyfikator eventu.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Opcjonalne metadane eventu.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

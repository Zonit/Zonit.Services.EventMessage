namespace Zonit.Messaging.Events;

/// <summary>
/// Model danych przekazywanych wewnêtrznie w systemie eventów.
/// </summary>
/// <typeparam name="TData">Typ danych eventu</typeparam>
internal sealed class EventPayload<TData>
{
    /// <summary>
    /// Dane eventu.
    /// </summary>
    public required TData Data { get; init; }

    /// <summary>
    /// Token anulowania dla operacji.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}

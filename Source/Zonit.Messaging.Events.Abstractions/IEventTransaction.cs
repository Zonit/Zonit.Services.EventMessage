namespace Zonit.Messaging.Events;

/// <summary>
/// Transakcja eventów - grupuje eventy do sekwencyjnego przetwarzania.
/// Eventy s¹ publikowane dopiero po wywo³aniu CommitAsync().
/// </summary>
public interface IEventTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Dodaje event do transakcji.
    /// </summary>
    /// <typeparam name="TEvent">Typ eventu</typeparam>
    /// <param name="payload">Dane eventu</param>
    IEventTransaction Enqueue<TEvent>(TEvent payload) where TEvent : notnull;

    /// <summary>
    /// Dodaje event z okreœlon¹ nazw¹ do transakcji.
    /// </summary>
    /// <param name="eventName">Nazwa eventu</param>
    /// <param name="payload">Dane eventu</param>
    IEventTransaction Enqueue(string eventName, object payload);

    /// <summary>
    /// Zatwierdza i publikuje wszystkie eventy w transakcji.
    /// Eventy s¹ przetwarzane sekwencyjnie w kolejnoœci dodania.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Czeka na zakoñczenie przetwarzania wszystkich eventów.
    /// </summary>
    Task WaitForCompletionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Liczba eventów w transakcji.
    /// </summary>
    int Count { get; }
}

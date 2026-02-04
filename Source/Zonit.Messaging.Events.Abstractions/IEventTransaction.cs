namespace Zonit.Messaging.Events;

/// <summary>
/// Transakcja eventów - grupuje eventy do sekwencyjnego przetwarzania.
/// Eventy są publikowane automatycznie podczas Dispose/DisposeAsync,
/// lub mogą być wcześniej zatwierdzone przez CommitAsync().
/// </summary>
/// <remarks>
/// <para>
/// <strong>Użycie z <c>using</c> (synchroniczne):</strong><br/>
/// Eventy są wysyłane synchronicznie podczas Dispose. Blokuje wątek do zakończenia.
/// </para>
/// <code>
/// using (var tx = eventProvider.CreateTransaction())
/// {
///     tx.Enqueue(new MyEvent());
/// } // Eventy wysłane synchronicznie
/// </code>
/// 
/// <para>
/// <strong>Użycie z <c>await using</c> (asynchroniczne):</strong><br/>
/// Eventy są wysyłane asynchronicznie podczas DisposeAsync. Nie blokuje wątku.
/// </para>
/// <code>
/// await using (var tx = eventProvider.CreateTransaction())
/// {
///     tx.Enqueue(new MyEvent());
/// } // Eventy wysłane asynchronicznie
/// </code>
/// 
/// <para>
/// Obie wersje działają poprawnie - eventy są zawsze wysyłane przy zakończeniu bloku.
/// W kodzie async preferuj <c>await using</c> dla lepszej wydajności.
/// </para>
/// </remarks>
public interface IEventTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Dodaje event do transakcji.
    /// </summary>
    /// <typeparam name="TEvent">Typ eventu</typeparam>
    /// <param name="payload">Dane eventu</param>
    IEventTransaction Enqueue<TEvent>(TEvent payload) where TEvent : notnull;

    /// <summary>
    /// Dodaje event z określoną nazwą do transakcji.
    /// </summary>
    /// <param name="eventName">Nazwa eventu</param>
    /// <param name="payload">Dane eventu</param>
    IEventTransaction Enqueue(string eventName, object payload);

    /// <summary>
    /// Zatwierdza i publikuje wszystkie eventy w transakcji.
    /// Eventy są przetwarzane sekwencyjnie w kolejności dodania.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Czeka na zakończenie przetwarzania wszystkich eventów.
    /// </summary>
    Task WaitForCompletionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Liczba eventów w transakcji.
    /// </summary>
    int Count { get; }
}

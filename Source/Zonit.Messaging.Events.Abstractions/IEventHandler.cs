namespace Zonit.Messaging.Events;

/// <summary>
/// Handler obsługujący określony typ eventu.
/// Każdy event może mieć wiele handlerów (fan-out).
/// </summary>
/// <remarks>
/// <para>
/// Implementuj <see cref="HandleAsync(TEvent, CancellationToken)"/> aby zdefiniować logikę
/// obsługi eventu. Dodatkowo możesz nadpisać domyślne właściwości subskrypcji
/// (<see cref="WorkerCount"/>, <see cref="Timeout"/>, <see cref="ContinueOnError"/>)
/// deklarując je jako zwykłe publiczne właściwości w klasie - kompilator potraktuje
/// je jako implicit interface implementation i nadpiszą wartości z DIM defaults.
/// </para>
/// <para>
/// Te właściwości są czytane raz przy starcie aplikacji (gdy subskrypcja jest podpinana
/// przez <c>EventHandlerRegistrationHostedService</c>). Późniejsze zmiany nie mają wpływu
/// na aktywną subskrypcję.
/// </para>
/// <para>
/// Priorytet konfiguracji subskrypcji:
/// <list type="number">
///   <item>Opcje przekazane jawnie do <c>AddEvent&lt;THandler, TEvent&gt;(opts =&gt; ...)</c>.</item>
///   <item>Wartości właściwości tego interfejsu (DIM defaults lub klasowy override).</item>
///   <item>Domyślne wartości <see cref="EventSubscriptionOptions"/>.</item>
/// </list>
/// </para>
/// <example>
/// <code>
/// public class LongRunningOrderHandler : IEventHandler&lt;OrderPlacedEvent&gt;
/// {
///     public TimeSpan Timeout =&gt; TimeSpan.FromMinutes(10);
///     public int WorkerCount =&gt; 2;
///
///     public Task HandleAsync(OrderPlacedEvent data, CancellationToken ct)
///         =&gt; _processor.ProcessAsync(data, ct);
/// }
/// </code>
/// </example>
/// </remarks>
/// <typeparam name="TEvent">Typ eventu do obsługi</typeparam>
public interface IEventHandler<TEvent> where TEvent : notnull
{
    /// <summary>
    /// Obsługuje event.
    /// </summary>
    /// <param name="data">Dane eventu</param>
    /// <param name="cancellationToken">Token anulowania dla operacji</param>
    /// <returns>Task reprezentujący operację asynchroniczną</returns>
    Task HandleAsync(TEvent data, CancellationToken cancellationToken);

    /// <summary>
    /// Liczba równoległych workerów konsumujących kanał eventów tego typu.
    /// Default: 10. Nadpisz w klasie aby zwiększyć/zmniejszyć równoległość.
    /// </summary>
    int WorkerCount => 10;

    /// <summary>
    /// Maksymalny czas wykonania pojedynczej invocation handlera.
    /// Default: 5 minut. Nadpisz w klasie dla szybkich handlerów (np. notyfikacje UI)
    /// lub long-running handlerów (np. data import, ML inference).
    /// </summary>
    TimeSpan Timeout => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Czy kontynuować przetwarzanie kanału po wyjątku w handlerze.
    /// Default: true (loguje błąd i drenuje dalej kolejkę).
    /// </summary>
    bool ContinueOnError => true;

    /// <summary>
    /// Maksymalna liczba zbuforowanych eventów tego typu oczekujących na przetworzenie.
    /// Default: <c>null</c> = bez limitu. Ustaw wartość, aby ograniczyć zużycie pamięci, gdy
    /// producent może trwale wyprzedzać workerów — po przekroczeniu limitu nadmiarowe eventy
    /// są odrzucane z ostrzeżeniem w logu (publikacja jest nieblokująca).
    /// </summary>
    int? Capacity => null;
}
namespace Zonit.Messaging.Events;

/// <summary>
/// Manager eventów - wewnętrzny serwis zarządzający subskrypcjami i publikacją.
/// </summary>
public interface IEventManager
{
    /// <summary>
    /// Publikuje event (fire-and-forget - nie czeka na zakończenie handlerów).
    /// </summary>
    /// <typeparam name="TEvent">Typ eventu</typeparam>
    /// <param name="payload">Dane eventu</param>
    void Publish<TEvent>(TEvent payload) where TEvent : notnull;

    /// <summary>
    /// Publikuje event z określoną nazwą (fire-and-forget).
    /// </summary>
    /// <param name="eventName">Nazwa eventu</param>
    /// <param name="payload">Dane eventu</param>
    void Publish(string eventName, object payload);

    /// <summary>
    /// Publikuje event i czeka na zakończenie wszystkich handlerów.
    /// Używane przez transakcje do sekwencyjnego przetwarzania.
    /// </summary>
    /// <param name="eventName">Nazwa eventu</param>
    /// <param name="payload">Dane eventu</param>
    /// <param name="cancellationToken">Token anulowania</param>
    Task PublishAndWaitAsync(string eventName, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subskrybuje handler do określonego typu eventu.
    /// </summary>
    /// <typeparam name="TEvent">Typ eventu</typeparam>
    /// <param name="handler">Funkcja obsługująca event</param>
    /// <param name="options">Opcje subskrypcji</param>
    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler, EventSubscriptionOptions? options = null)
        where TEvent : notnull;

    /// <summary>
    /// Subskrybuje handler do eventu o określonej nazwie.
    /// </summary>
    /// <param name="eventName">Nazwa eventu</param>
    /// <param name="handler">Funkcja obsługująca event</param>
    /// <param name="options">Opcje subskrypcji</param>
    void Subscribe(string eventName, Func<object, CancellationToken, Task> handler, EventSubscriptionOptions? options = null);

    /// <summary>
    /// Tworzy nową transakcję eventów.
    /// </summary>
    IEventTransaction CreateTransaction();
}

/// <summary>
/// Opcje subskrypcji eventu.
/// </summary>
/// <remarks>
/// Te opcje są stosowane gdy handler jest rejestrowany ręcznie przez <c>IEventManager.Subscribe(...)</c>
/// lub przez <c>AddEvent&lt;THandler, TEvent&gt;(opts =&gt; ...)</c>. Gdy handler dziedziczy po
/// <see cref="EventHandler{TEvent}"/>, jego properties (<c>WorkerCount</c>, <c>Timeout</c>,
/// <c>ContinueOnError</c>) mają pierwszeństwo nad domyślnymi wartościami tej klasy, chyba że
/// opcje zostały przekazane jawnie do <c>AddEvent</c>.
/// </remarks>
public sealed class EventSubscriptionOptions
{
    /// <summary>
    /// Liczba równoległych workerów przetwarzających eventy.
    /// Default: 10
    /// </summary>
    public int WorkerCount { get; init; } = 10;

    /// <summary>
    /// Maksymalny czas wykonania handlera.
    /// Default: 5 minut.
    /// </summary>
    /// <remarks>
    /// Bumped z 30s do 5 min, aby pasować do <c>TaskHandler</c> i <c>ScheduleOptions</c>.
    /// Dla szybkich handlerów (np. notyfikacje UI) ustaw mniejszą wartość przez
    /// <see cref="EventHandler{TEvent}.Timeout"/> override lub przez jawne opcje.
    /// </remarks>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Czy kontynuować przetwarzanie po błędzie.
    /// Default: true
    /// </summary>
    public bool ContinueOnError { get; init; } = true;
}
namespace Zonit.Messaging.Events;

/// <summary>
/// Manager eventów - wewnêtrzny serwis zarz¹dzaj¹cy subskrypcjami i publikacj¹.
/// </summary>
public interface IEventManager
{
    /// <summary>
    /// Publikuje event.
    /// </summary>
    /// <typeparam name="TEvent">Typ eventu</typeparam>
    /// <param name="payload">Dane eventu</param>
    void Publish<TEvent>(TEvent payload) where TEvent : notnull;

    /// <summary>
    /// Publikuje event z okreœlon¹ nazw¹.
    /// </summary>
    /// <param name="eventName">Nazwa eventu</param>
    /// <param name="payload">Dane eventu</param>
    void Publish(string eventName, object payload);

    /// <summary>
    /// Subskrybuje handler do okreœlonego typu eventu.
    /// </summary>
    /// <typeparam name="TEvent">Typ eventu</typeparam>
    /// <param name="handler">Funkcja obs³uguj¹ca event</param>
    /// <param name="options">Opcje subskrypcji</param>
    void Subscribe<TEvent>(Func<EventPayload<TEvent>, Task> handler, EventSubscriptionOptions? options = null) 
        where TEvent : notnull;

    /// <summary>
    /// Subskrybuje handler do eventu o okreœlonej nazwie.
    /// </summary>
    /// <param name="eventName">Nazwa eventu</param>
    /// <param name="handler">Funkcja obs³uguj¹ca event</param>
    /// <param name="options">Opcje subskrypcji</param>
    void Subscribe(string eventName, Func<EventPayload<object>, Task> handler, EventSubscriptionOptions? options = null);

    /// <summary>
    /// Tworzy now¹ transakcjê eventów.
    /// </summary>
    IEventTransaction CreateTransaction();
}

/// <summary>
/// Opcje subskrypcji eventu.
/// </summary>
public sealed class EventSubscriptionOptions
{
    /// <summary>
    /// Liczba równoleg³ych workerów przetwarzaj¹cych eventy.
    /// Default: 10
    /// </summary>
    public int WorkerCount { get; init; } = 10;

    /// <summary>
    /// Maksymalny czas wykonania handlera.
    /// Default: 30 sekund
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Czy kontynuowaæ przetwarzanie po b³êdzie.
    /// Default: true
    /// </summary>
    public bool ContinueOnError { get; init; } = true;
}

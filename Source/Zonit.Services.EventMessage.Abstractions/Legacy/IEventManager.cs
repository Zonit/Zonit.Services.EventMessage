using System.ComponentModel;

namespace Zonit.Services.EventMessage;

/// <summary>
/// [LEGACY] Manager eventów - wewnêtrzny serwis.
/// </summary>
/// <remarks>
/// <para><b>Ten interfejs jest przestarza³y.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Zamiast: <c>using Zonit.Services.EventMessage;</c><br/>
/// U¿yj: <c>using Zonit.Messaging.Events;</c><br/>
/// <br/>
/// Zamiast: <c>IEventManager</c> z namespace <c>Zonit.Services.EventMessage</c><br/>
/// U¿yj: <c>IEventManager</c> z namespace <c>Zonit.Messaging.Events</c>
/// </para>
/// </remarks>
[Obsolete("Use Zonit.Messaging.Events.IEventManager instead. Change namespace from 'Zonit.Services.EventMessage' to 'Zonit.Messaging.Events'.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventManager
{
    /// <summary>
    /// Publikuje event.
    /// </summary>
    void Publish<TEvent>(TEvent payload) where TEvent : notnull;

    /// <summary>
    /// Publikuje event z okreœlon¹ nazw¹.
    /// </summary>
    void Publish(string eventName, object payload);

    /// <summary>
    /// Subskrybuje handler do eventu.
    /// </summary>
    void Subscribe<TEvent>(
        Func<PayloadModel<TEvent>, Task> handler,
        int workerCount = 10,
        TimeSpan? timeout = null) where TEvent : notnull;

    /// <summary>
    /// Subskrybuje handler do eventu z okreœlon¹ nazw¹.
    /// </summary>
    void Subscribe(
        string eventName,
        Func<PayloadModel<object>, Task> handler,
        int workerCount = 10,
        TimeSpan? timeout = null);

    /// <summary>
    /// Sprawdza czy manager wspiera generyczn¹ subskrypcjê.
    /// </summary>
    bool SupportsGenericSubscription() => true;

    /// <summary>
    /// Tworzy now¹ transakcjê eventów.
    /// </summary>
    IEventTransaction CreateTransaction();
}

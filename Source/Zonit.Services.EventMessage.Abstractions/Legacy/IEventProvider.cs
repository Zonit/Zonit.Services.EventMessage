using System.ComponentModel;

namespace Zonit.Services.EventMessage;

/// <summary>
/// [LEGACY] Provider do publikowania eventów.
/// </summary>
/// <remarks>
/// <para><b>Ten interfejs jest przestarza³y.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Zamiast: <c>using Zonit.Services.EventMessage;</c><br/>
/// U¿yj: <c>using Zonit.Messaging.Events;</c><br/>
/// <br/>
/// Zamiast: <c>IEventProvider</c> z namespace <c>Zonit.Services.EventMessage</c><br/>
/// U¿yj: <c>IEventProvider</c> z namespace <c>Zonit.Messaging.Events</c>
/// </para>
/// </remarks>
[Obsolete("Use Zonit.Messaging.Events.IEventProvider instead. Change namespace from 'Zonit.Services.EventMessage' to 'Zonit.Messaging.Events'.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventProvider
{
    /// <summary>
    /// Publikuje event do wszystkich subskrybentów.
    /// </summary>
    void Publish<TEvent>(TEvent payload) where TEvent : notnull;

    /// <summary>
    /// Publikuje event z okreœlon¹ nazw¹.
    /// </summary>
    void Publish(string eventName, object payload);

    /// <summary>
    /// Tworzy now¹ transakcjê eventów.
    /// </summary>
    [Obsolete("Use Zonit.Messaging.Events.IEventProvider.CreateTransaction() instead.")]
    IEventTransaction Transaction();
}

/// <summary>
/// [LEGACY] Transakcja eventów.
/// </summary>
[Obsolete("Use Zonit.Messaging.Events.IEventTransaction instead.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventTransaction : IDisposable
{
    /// <summary>
    /// Dodaje event do transakcji.
    /// </summary>
    void Enqueue<TEvent>(TEvent payload) where TEvent : notnull;

    /// <summary>
    /// Dodaje event z okreœlon¹ nazw¹ do transakcji.
    /// </summary>
    void Enqueue(string eventName, object payload);
}

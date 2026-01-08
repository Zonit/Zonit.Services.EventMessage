namespace Zonit.Messaging.Events;

/// <summary>
/// Handler obs³uguj¹cy okreœlony typ eventu.
/// Ka¿dy event mo¿e mieæ wiele handlerów (fan-out).
/// </summary>
/// <typeparam name="TEvent">Typ eventu do obs³ugi</typeparam>
public interface IEventHandler<TEvent> where TEvent : notnull
{
    /// <summary>
    /// Obs³uguje event.
    /// </summary>
    /// <param name="payload">Dane eventu z metadanymi</param>
    /// <returns>Task reprezentuj¹cy operacjê asynchroniczn¹</returns>
    Task HandleAsync(EventPayload<TEvent> payload);
}

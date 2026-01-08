namespace Zonit.Messaging.Events;

/// <summary>
/// Provider do publikowania eventów (pub/sub pattern).
/// Eventy s¹ przetwarzane asynchronicznie przez subskrybentów.
/// </summary>
public interface IEventProvider
{
    /// <summary>
    /// Publikuje event do wszystkich subskrybentów.
    /// Nazwa eventu jest automatycznie okreœlana na podstawie typu TEvent.
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
    /// Tworzy now¹ transakcjê eventów.
    /// Eventy w transakcji s¹ przetwarzane sekwencyjnie po zatwierdzeniu.
    /// </summary>
    /// <returns>Transakcja eventów</returns>
    IEventTransaction CreateTransaction();
}

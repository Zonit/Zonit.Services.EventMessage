namespace Zonit.Services.EventMessage;

public interface IEventManager
{
    void Publish(object payload, string? eventName = null);
    void Subscribe(string eventName, Func<PayloadModel, Task> handler, int? workers = null, TimeSpan? timeout = null);
    void Subscribe<TModel>(Func<PayloadModel<TModel>, Task> handler, int? workers = null, TimeSpan? timeout = null);

    /// <summary>
    /// Tworzy nową transakcję zdarzeń, która zapewnia sekwencyjne wykonanie zdarzeń
    /// </summary>
    /// <returns>Obiekt transakcji zdarzeń</returns>
    IEventTransaction Transaction();
}
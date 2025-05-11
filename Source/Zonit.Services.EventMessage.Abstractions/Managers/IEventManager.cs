namespace Zonit.Services.EventMessage;

public interface IEventManager
{
    void Publish(object payload, string? eventName = null);
    void Subscribe(string eventName, Func<PayloadModel, Task> handler, int? workers = null, TimeSpan? timeout = null);
    void Subscribe<TModel>(Func<PayloadModel<TModel>, Task> handler, int? workers = null, TimeSpan? timeout = null);
}
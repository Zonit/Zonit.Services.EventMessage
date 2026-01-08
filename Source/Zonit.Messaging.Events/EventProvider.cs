namespace Zonit.Messaging.Events;

/// <summary>
/// Domyœlna implementacja IEventProvider.
/// Wrapper nad IEventManager dla publicznego API.
/// </summary>
public sealed class EventProvider : IEventProvider
{
    private readonly IEventManager _eventManager;

    public EventProvider(IEventManager eventManager)
    {
        _eventManager = eventManager;
    }

    public void Publish<TEvent>(TEvent payload) where TEvent : notnull
    {
        _eventManager.Publish(payload);
    }

    public void Publish(string eventName, object payload)
    {
        _eventManager.Publish(eventName, payload);
    }

    public IEventTransaction CreateTransaction()
    {
        return _eventManager.CreateTransaction();
    }
}

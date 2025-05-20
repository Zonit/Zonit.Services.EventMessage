namespace Zonit.Services.EventMessage.Services;

public class EventProviderService(IEventManager eventManager) : IEventProvider
{
    public void Publish(object payload)
        => eventManager.Publish(payload);
    
    public void Publish(string eventName, object payload)
        => eventManager.Publish(payload, eventName);

    public IEventTransaction Transaction()
        => eventManager.Transaction();
}

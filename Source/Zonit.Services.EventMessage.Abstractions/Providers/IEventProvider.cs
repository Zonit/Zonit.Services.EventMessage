namespace Zonit.Services.EventMessage;

public interface IEventProvider
{
    void Publish(object payload);
    void Publish(string eventName, object payload);
}
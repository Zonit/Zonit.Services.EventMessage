namespace Zonit.Services.EventMessage;

public interface IEventProvider
{
    void Publish(string eventName, object payload);
    void Subscribe(string eventName, int worker, Func<PayloadModel, Task> handler);
}
//public interface IEventBus2
//{
//    void Publish(string eventName, object payload);
//    void Publish(string eventName, EntitiesModel entities, object payload);
//    void Subscribe(string eventName, Func<object, Task> handler);
//    void Subscribe(string eventName, int worker, Func<object, Task> handler);
//    void Subscribe(string eventName, int worker, TimeSpan timeout, Func<object, Task> handler);
//}
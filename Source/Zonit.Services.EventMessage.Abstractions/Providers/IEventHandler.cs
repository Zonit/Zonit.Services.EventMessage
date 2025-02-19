namespace Zonit.Services.EventMessage;

public interface IEventHandler
{
    void Subscribe(IServiceProvider serviceProvider);
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Zonit.Services.EventMessage.Services;

internal class EventHandlersHostedService(IServiceProvider _serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var eventBus = _serviceProvider.GetRequiredService<IEventProvider>();
        var logger = _serviceProvider.GetRequiredService<ILogger<EventBase>>();
        var handlers = _serviceProvider.GetServices<IEventHandler>();

        foreach (var handler in handlers)
        {
            handler.GetType().GetProperty("EventBus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(handler, eventBus);
            handler.GetType().GetProperty("Logger", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(handler, logger);

            handler.Subscribe();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
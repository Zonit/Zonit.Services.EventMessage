using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Zonit.Services.EventMessage.Services;

internal class EventHandlersHostedService(IServiceProvider _serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventProvider>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<EventBase>>();
            var handlers = scope.ServiceProvider.GetServices<IEventHandler>();

            foreach (var handler in handlers)
            {
                handler.GetType().GetProperty("EventBus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(handler, eventBus);
                handler.GetType().GetProperty("Logger", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(handler, logger);

                handler.Subscribe();
            }
        }

        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
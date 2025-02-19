using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Zonit.Services.EventMessage.Services;

internal class EventHandlersHostedService(IServiceProvider _serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>();

        foreach (var handler in handlers)
            if (handler is EventBase eventBaseHandler)
                eventBaseHandler.Subscribe(scope.ServiceProvider);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace Zonit.Services.EventMessage;

public abstract class EventBase : IEventHandler
{
    /// <summary>
    /// Name of the event to subscribe to.
    /// </summary>
    protected abstract string EventName { get; }

    /// <summary>
    /// Number of workers to handle the event.
    /// </summary>
    protected virtual int EventWorker { get; } = 10;

    /// <summary>
    /// Handle code
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task HandleAsync(object data, CancellationToken cancellationToken);

    public void Subscribe(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var eventBus = serviceProvider.GetRequiredService<IEventProvider>();
        var logger = serviceProvider.GetRequiredService<ILogger<EventBase>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        eventBus.Subscribe(EventName, EventWorker, async payload =>
        {
            logger.LogInformation("Subscribed {EventName}", EventName);

            using var scope = scopeFactory.CreateScope();

            try
            {
                if (scope.ServiceProvider.GetRequiredService(GetType()) is not EventBase handler)
                    return;

                await handler.HandleAsync(payload.Data, payload.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event '{EventName}' failed", EventName);
            }
        });
    }
}
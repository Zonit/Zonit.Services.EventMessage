using Microsoft.Extensions.Logging;

namespace Zonit.Services.EventMessage;

public abstract class EventBase : IEventHandler
{
    protected IEventProvider EventBus { get; set; } = null!;
    protected ILogger<EventBase> Logger { get; set; } = null!;

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
    protected abstract Task HandleAsync(PayloadModel payload, CancellationToken cancellationToken);

    public void Subscribe()
    {
        EventBus.Subscribe(EventName, EventWorker, async payload =>
        {
            try
            {
                Logger.LogInformation("Subscribed: {EventName}", EventName);
                await HandleAsync(payload, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Event '{EventName}' failed", EventName);
            }
        });
    }
}
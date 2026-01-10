using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Zonit.Messaging.Events.Hosting;

/// <summary>
/// Hosted service do automatycznej rejestracji handlerów eventów przy starcie aplikacji.
/// Skanuje DI po IEventHandler&lt;T&gt; i automatycznie subskrybuje do odpowiednich eventów.
/// </summary>
public sealed class EventHandlerRegistrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventManager _eventManager;
    private readonly ILogger<EventHandlerRegistrationHostedService> _logger;

    public EventHandlerRegistrationHostedService(
        IServiceProvider serviceProvider,
        IEventManager eventManager,
        ILogger<EventHandlerRegistrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _eventManager = eventManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting event handler registration...");

        using var scope = _serviceProvider.CreateScope();
        var registrations = scope.ServiceProvider.GetServices<EventHandlerRegistration>();

        foreach (var registration in registrations)
        {
            registration.Subscribe(_eventManager, _serviceProvider);
            _logger.LogDebug("Subscribed handler for event type '{EventType}'", registration.EventType.Name);
        }

        _logger.LogInformation("Event handler registration completed");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Abstrakcyjna rejestracja handlera eventów.
/// </summary>
public abstract class EventHandlerRegistration
{
    public abstract Type EventType { get; }
    public abstract void Subscribe(IEventManager eventManager, IServiceProvider serviceProvider);
}

/// <summary>
/// Typowana rejestracja handlera eventów.
/// </summary>
public sealed class EventHandlerRegistration<TEvent> : EventHandlerRegistration where TEvent : notnull
{
    private readonly EventSubscriptionOptions _options;

    public EventHandlerRegistration(EventSubscriptionOptions? options = null)
    {
        _options = options ?? new EventSubscriptionOptions();
    }

    public override Type EventType => typeof(TEvent);

    public override void Subscribe(IEventManager eventManager, IServiceProvider serviceProvider)
    {
        eventManager.Subscribe<TEvent>(async (data, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();

            foreach (var handler in handlers)
            {
                await handler.HandleAsync(data, cancellationToken);
            }
        }, _options);
    }
}

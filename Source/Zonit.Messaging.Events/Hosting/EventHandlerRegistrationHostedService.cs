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
/// <remarks>
/// Priorytet opcji przy subskrypcji:
/// <list type="number">
///   <item>Opcje przekazane jawnie do <c>AddEvent&lt;THandler, TEvent&gt;(opts =&gt; ...)</c>.</item>
///   <item>Properties zadeklarowane na handlerze implementującym <see cref="IEventHandler{TEvent}"/> (override default-interface members).</item>
///   <item>Domyślne wartości <see cref="EventSubscriptionOptions"/>.</item>
/// </list>
/// </remarks>
public sealed class EventHandlerRegistration<TEvent> : EventHandlerRegistration where TEvent : notnull
{
    private readonly EventSubscriptionOptions? _options;

    public EventHandlerRegistration(EventSubscriptionOptions? options = null)
    {
        _options = options;
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
        }, GetOptionsFromHandler(serviceProvider));
    }

    private EventSubscriptionOptions GetOptionsFromHandler(IServiceProvider serviceProvider)
    {
        // 1. Explicit options passed to AddEvent take precedence
        if (_options is not null)
            return _options;

        // 2. Read subscription defaults straight off the handler. WorkerCount/Timeout/
        //    ContinueOnError are default-interface members on IEventHandler<TEvent>; a handler
        //    that declares matching public properties overrides them as implicit interface
        //    implementations, otherwise the interface defaults apply.
        using var scope = serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetService<IEventHandler<TEvent>>();

        if (handler is not null)
        {
            return new EventSubscriptionOptions
            {
                WorkerCount = handler.WorkerCount,
                Timeout = handler.Timeout,
                ContinueOnError = handler.ContinueOnError,
                Capacity = handler.Capacity
            };
        }

        // 3. Fallback to library defaults
        return new EventSubscriptionOptions();
    }
}
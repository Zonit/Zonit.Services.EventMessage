using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Zonit.Messaging.Events;

/// <summary>
/// Domyœlna implementacja IEventManager.
/// U¿ywa Channel do asynchronicznego przetwarzania eventów.
/// </summary>
public sealed class EventManager : IEventManager, IDisposable
{
    private readonly ConcurrentDictionary<string, List<EventSubscription>> _subscriptions = new();
    private readonly ILogger<EventManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    public EventManager(ILogger<EventManager> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public void Publish<TEvent>(TEvent payload) where TEvent : notnull
    {
        var eventName = GetEventName<TEvent>();
        Publish(eventName, payload);
    }

    public void Publish(string eventName, object payload)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_subscriptions.TryGetValue(eventName, out var subscriptions))
        {
            _logger.LogDebug("No subscribers for event '{EventName}'", eventName);
            return;
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                subscription.Enqueue(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueueing event '{EventName}'", eventName);
            }
        }
    }

    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler, EventSubscriptionOptions? options = null) 
        where TEvent : notnull
    {
        var eventName = GetEventName<TEvent>();
        var opts = options ?? new EventSubscriptionOptions();

        var subscription = new EventSubscription<TEvent>(handler, opts, _logger);
        
        _subscriptions.AddOrUpdate(
            eventName,
            _ => [subscription],
            (_, list) => { list.Add(subscription); return list; });

        _logger.LogInformation(
            "Subscribed to event '{EventName}' with {WorkerCount} workers", 
            eventName, 
            opts.WorkerCount);
    }

    public void Subscribe(string eventName, Func<object, CancellationToken, Task> handler, EventSubscriptionOptions? options = null)
    {
        var opts = options ?? new EventSubscriptionOptions();
        var subscription = new EventSubscription<object>(handler, opts, _logger);

        _subscriptions.AddOrUpdate(
            eventName,
            _ => [subscription],
            (_, list) => { list.Add(subscription); return list; });

        _logger.LogInformation(
            "Subscribed to event '{EventName}' with {WorkerCount} workers", 
            eventName, 
            opts.WorkerCount);
    }

    public IEventTransaction CreateTransaction()
    {
        return new EventTransaction(this, _logger);
    }

    private static string GetEventName<TEvent>() => typeof(TEvent).FullName ?? typeof(TEvent).Name;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var subscriptions in _subscriptions.Values)
        {
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }
        _subscriptions.Clear();
    }
}

/// <summary>
/// Wewnêtrzna klasa reprezentuj¹ca subskrypcjê.
/// </summary>
internal abstract class EventSubscription : IDisposable
{
    public abstract void Enqueue(object payload);
    public abstract void Dispose();
}

/// <summary>
/// Typowana subskrypcja eventu.
/// </summary>
internal sealed class EventSubscription<TEvent> : EventSubscription where TEvent : notnull
{
    private readonly Channel<TEvent> _channel;
    private readonly Func<TEvent, CancellationToken, Task> _handler;
    private readonly EventSubscriptionOptions _options;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task[] _workers;

    public EventSubscription(
        Func<TEvent, CancellationToken, Task> handler, 
        EventSubscriptionOptions options, 
        ILogger logger)
    {
        _handler = handler;
        _options = options;
        _logger = logger;

        _channel = Channel.CreateUnbounded<TEvent>(new UnboundedChannelOptions
        {
            SingleReader = options.WorkerCount == 1,
            SingleWriter = false
        });

        _workers = new Task[options.WorkerCount];
        for (int i = 0; i < options.WorkerCount; i++)
        {
            _workers[i] = ProcessEventsAsync(_cts.Token);
        }
    }

    public override void Enqueue(object payload)
    {
        if (payload is TEvent typedPayload)
        {
            _channel.Writer.TryWrite(typedPayload);
        }
        else
        {
            _logger.LogWarning(
                "Expected event type '{ExpectedType}', got '{ActualType}'",
                typeof(TEvent).Name,
                payload.GetType().Name);
        }
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var data in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(_options.Timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCts.Token, 
                    cancellationToken);

                await _handler(data, linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event of type '{EventType}'", typeof(TEvent).Name);
                
                if (!_options.ContinueOnError)
                    throw;
            }
        }
    }

    public override void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.Complete();
        
        try
        {
            Task.WhenAll(_workers).Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore cancellation exceptions during shutdown
        }

        _cts.Dispose();
    }
}

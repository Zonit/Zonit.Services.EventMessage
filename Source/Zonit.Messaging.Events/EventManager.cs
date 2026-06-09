using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Zonit.Messaging.Events;

/// <summary>
/// Domyślna implementacja IEventManager.
/// Używa Channel do asynchronicznego przetwarzania eventów.
/// Obsługuje ambient transaction - eventy publikowane podczas aktywnej transakcji są kolejkowane.
/// </summary>
public sealed class EventManager : IEventManager, IDisposable
{
    private readonly ConcurrentDictionary<string, List<EventSubscription>> _subscriptions = new();
    private readonly ILogger<EventManager> _logger;
    private bool _disposed;

    /// <summary>
    /// Ambient transaction - przechowuje aktywną transakcję dla bieżącego async flow.
    /// </summary>
    private static readonly AsyncLocal<IEventTransaction?> _currentTransaction = new();

    public EventManager(ILogger<EventManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the current ambient transaction for this async flow.
    /// </summary>
    internal static IEventTransaction? CurrentTransaction
    {
        get => _currentTransaction.Value;
        set => _currentTransaction.Value = value;
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

        // Jeśli jest aktywna transakcja, kolejkuj event zamiast publikować bezpośrednio
        if (_currentTransaction.Value is { } transaction)
        {
            transaction.Enqueue(eventName, payload);
            _logger.LogDebug("Event '{EventName}' enqueued in active transaction", eventName);
            return;
        }

        PublishDirect(eventName, payload);
    }

    /// <summary>
    /// Bezpośrednia publikacja eventu (pomija ambient transaction).
    /// Używane wewnętrznie przez EventTransaction podczas commit.
    /// </summary>
    internal void PublishDirect(string eventName, object payload)
    {
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

    /// <inheritdoc />
    public async Task PublishAndWaitAsync(string eventName, object payload, CancellationToken cancellationToken = default)
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
                await subscription.ExecuteAsync(payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing handler for event '{EventName}'", eventName);
            }
        }
    }

    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler, EventSubscriptionOptions? options = null)
        where TEvent : notnull
    {
        var eventName = GetEventName<TEvent>();
        var opts = options ?? new EventSubscriptionOptions();

        var subscription = new EventSubscription<TEvent>(handler, opts, _logger);

        // Copy-on-write: publishers iterate the captured list reference without locking, so a
        // concurrent Subscribe must swap in a new list rather than mutate the existing one.
        _subscriptions.AddOrUpdate(
            eventName,
            _ => [subscription],
            (_, list) => [.. list, subscription]);

        _logger.LogInformation(
            "Subscribed to event '{EventName}' with {WorkerCount} workers",
            eventName,
            opts.WorkerCount);
    }

    public void Subscribe(string eventName, Func<object, CancellationToken, Task> handler, EventSubscriptionOptions? options = null)
    {
        var opts = options ?? new EventSubscriptionOptions();
        var subscription = new EventSubscription<object>(handler, opts, _logger);

        // Copy-on-write: publishers iterate the captured list reference without locking, so a
        // concurrent Subscribe must swap in a new list rather than mutate the existing one.
        _subscriptions.AddOrUpdate(
            eventName,
            _ => [subscription],
            (_, list) => [.. list, subscription]);

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
/// Wewnętrzna klasa reprezentująca subskrypcję.
/// </summary>
internal abstract class EventSubscription : IDisposable
{
    public abstract void Enqueue(object payload);
    public abstract Task ExecuteAsync(object payload, CancellationToken cancellationToken);
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

        _channel = options.Capacity is int cap && cap > 0
            ? Channel.CreateBounded<TEvent>(new BoundedChannelOptions(cap)
            {
                SingleReader = options.WorkerCount == 1,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            })
            : Channel.CreateUnbounded<TEvent>(new UnboundedChannelOptions
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
            if (!_channel.Writer.TryWrite(typedPayload))
            {
                // Only reachable for a bounded channel that is full; unbounded always accepts.
                _logger.LogWarning(
                    "Event channel for '{EventType}' is full (capacity {Capacity}); event dropped.",
                    typeof(TEvent).Name,
                    _options.Capacity);
            }
        }
        else
        {
            _logger.LogWarning(
                "Expected event type '{ExpectedType}', got '{ActualType}'",
                typeof(TEvent).Name,
                payload.GetType().Name);
        }
    }

    public override async Task ExecuteAsync(object payload, CancellationToken cancellationToken)
    {
        if (payload is TEvent typedPayload)
        {
            if (_options.Timeout == Timeout.InfiniteTimeSpan)
            {
                await _handler(typedPayload, cancellationToken);
            }
            else
            {
                using var timeoutCts = new CancellationTokenSource(_options.Timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCts.Token,
                    cancellationToken);

                await _handler(typedPayload, linkedCts.Token);
            }
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
                if (_options.Timeout == Timeout.InfiniteTimeSpan)
                {
                    await _handler(data, cancellationToken);
                }
                else
                {
                    using var timeoutCts = new CancellationTokenSource(_options.Timeout);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        timeoutCts.Token,
                        cancellationToken);

                    await _handler(data, linkedCts.Token);
                }
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

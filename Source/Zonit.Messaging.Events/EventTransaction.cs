using Microsoft.Extensions.Logging;

namespace Zonit.Messaging.Events;

/// <summary>
/// Implementacja transakcji eventów.
/// Grupuje eventy i przetwarza je sekwencyjnie po zatwierdzeniu.
/// </summary>
public sealed class EventTransaction : IEventTransaction
{
    private readonly IEventManager _eventManager;
    private readonly ILogger _logger;
    private readonly List<(string EventName, object Payload)> _events = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _committed;
    private bool _disposed;
    private Task? _completionTask;

    internal EventTransaction(IEventManager eventManager, ILogger logger)
    {
        _eventManager = eventManager;
        _logger = logger;
    }

    public int Count => _events.Count;

    public IEventTransaction Enqueue<TEvent>(TEvent payload) where TEvent : notnull
    {
        var eventName = typeof(TEvent).FullName ?? typeof(TEvent).Name;
        return Enqueue(eventName, payload);
    }

    public IEventTransaction Enqueue(string eventName, object payload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (_committed)
        {
            throw new InvalidOperationException("Cannot enqueue events after transaction has been committed.");
        }

        _events.Add((eventName, payload));
        return this;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_committed)
        {
            throw new InvalidOperationException("Transaction has already been committed.");
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_committed) return;
            _committed = true;

            _logger.LogDebug("Committing transaction with {EventCount} events", _events.Count);

            _completionTask = ProcessEventsSequentiallyAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ProcessEventsSequentiallyAsync(CancellationToken cancellationToken)
    {
        foreach (var (eventName, payload) in _events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _eventManager.Publish(eventName, payload);
                
                // Krótka pauza miêdzy eventami dla sekwencyjnoœci
                await Task.Delay(1, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing event '{EventName}' in transaction", eventName);
                throw;
            }
        }
    }

    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        if (_completionTask is not null)
        {
            await _completionTask.WaitAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
        _events.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_completionTask is not null)
        {
            try
            {
                await _completionTask;
            }
            catch
            {
                // Ignore exceptions during disposal
            }
        }

        _lock.Dispose();
        _events.Clear();
    }
}

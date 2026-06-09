using Microsoft.Extensions.Logging;

namespace Zonit.Messaging.Events;

/// <summary>
/// Implementacja transakcji eventów.
/// Grupuje eventy i przetwarza je sekwencyjnie po zatwierdzeniu (CommitAsync,
/// WaitForCompletionAsync lub przy zakończeniu bloku using/await using).
/// Automatycznie przechwytuje eventy publikowane przez IEventProvider.Publish() w czasie życia transakcji.
/// </summary>
/// <remarks>
/// W kodzie asynchronicznym preferuj <c>await using</c> (DisposeAsync) — synchroniczne
/// <c>using</c> musi zmostkować asynchroniczny commit i blokuje wątek do jego zakończenia.
/// </remarks>
public sealed class EventTransaction : IEventTransaction
{
    private readonly IEventManager _eventManager;
    private readonly ILogger _logger;
    private readonly List<(string EventName, object Payload)> _events = [];
    // Guards _events, _committed and _completionTask — the transaction is ambient across an
    // async flow, so Publish/Enqueue can be called from concurrent branches.
    private readonly object _gate = new();
    private readonly IEventTransaction? _previousTransaction;
    private bool _committed;
    private bool _disposed;
    private Task? _completionTask;

    internal EventTransaction(IEventManager eventManager, ILogger logger)
    {
        _eventManager = eventManager;
        _logger = logger;

        // Zapisz poprzednią transakcję (dla zagnieżdżonych) i ustaw bieżącą
        _previousTransaction = EventManager.CurrentTransaction;
        EventManager.CurrentTransaction = this;
    }

    public int Count
    {
        get { lock (_gate) return _events.Count; }
    }

    public IEventTransaction Enqueue<TEvent>(TEvent payload) where TEvent : notnull
    {
        var eventName = typeof(TEvent).FullName ?? typeof(TEvent).Name;
        return Enqueue(eventName, payload);
    }

    public IEventTransaction Enqueue(string eventName, object payload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (_committed)
                throw new InvalidOperationException("Cannot enqueue events after transaction has been committed.");

            _events.Add((eventName, payload));
        }

        return this;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        (string EventName, object Payload)[] batch;
        lock (_gate)
        {
            if (_committed)
                throw new InvalidOperationException("Transaction has already been committed.");

            _committed = true;
            batch = [.. _events];
            _completionTask = ProcessEventsSequentiallyAsync(batch, cancellationToken);
            _logger.LogDebug("Committing transaction with {EventCount} events", batch.Length);
            return _completionTask;
        }
    }

    private async Task ProcessEventsSequentiallyAsync(
        (string EventName, object Payload)[] batch,
        CancellationToken cancellationToken)
    {
        foreach (var (eventName, payload) in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Czeka na zakończenie wszystkich handlerów przed przejściem do następnego eventu
                await _eventManager.PublishAndWaitAsync(eventName, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing event '{EventName}' in transaction", eventName);
                throw;
            }
        }
    }

    /// <summary>
    /// Ensures the batch has been committed (committing now if the caller did not call
    /// CommitAsync explicitly) and waits for all events to finish processing.
    /// </summary>
    public async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Task completion;
        lock (_gate)
        {
            if (_completionTask is null)
            {
                // Caller awaited completion without an explicit commit — commit now so this
                // actually waits for the enqueued events (previously a silent no-op).
                _committed = true;
                var batch = _events.ToArray();
                _completionTask = ProcessEventsSequentiallyAsync(batch, cancellationToken);
            }
            completion = _completionTask;
        }

        await completion.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        Task? toAwait = null;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            // Przywróć poprzednią transakcję (dla zagnieżdżonych) — handlery odpalane podczas
            // commitu nie mają już dołączać do tej (kończonej) transakcji.
            EventManager.CurrentTransaction = _previousTransaction;

            if (!_committed && _events.Count > 0)
            {
                _committed = true;
                toAwait = _completionTask = ProcessEventsSequentiallyAsync([.. _events], CancellationToken.None);
            }
            else
            {
                toAwait = _completionTask;
            }
        }

        if (toAwait is not null)
        {
            try
            {
                // Sync bridge: offload to the thread pool to avoid sync-context deadlocks.
                // Prefer 'await using' (DisposeAsync) in async code to skip this block entirely.
                Task.Run(() => toAwait).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dispatching events during transaction Dispose");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? toAwait = null;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            EventManager.CurrentTransaction = _previousTransaction;

            if (!_committed && _events.Count > 0)
            {
                _committed = true;
                toAwait = _completionTask = ProcessEventsSequentiallyAsync([.. _events], CancellationToken.None);
            }
            else
            {
                toAwait = _completionTask;
            }
        }

        if (toAwait is not null)
        {
            try
            {
                await toAwait;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dispatching events during transaction DisposeAsync");
            }
        }
    }
}

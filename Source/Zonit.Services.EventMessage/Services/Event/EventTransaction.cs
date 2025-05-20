using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Zonit.Services.EventMessage.Services.Event;

/// <summary>
/// Implementacja transakcji zdarzeñ zapewniaj¹ca sekwencyjne wykonywanie zdarzeñ
/// </summary>
internal class EventTransaction : IEventTransaction
{
    private readonly EventManagerService _eventManager;
    // U¿ywamy ConcurrentQueue zamiast List dla thread-safety
    private readonly ConcurrentQueue<(object Payload, string? EventName)> _pendingEvents = new();
    private int _isDisposed;
    private int _isCommitted;

    public EventTransaction(EventManagerService eventManager)
    {
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));

        // Rejestracja tej transakcji jako aktywnej w bie¿¹cym kontekœcie
        EventTransactionContext.SetCurrent(this);
    }

    /// <summary>
    /// Dodaje zdarzenie do kolejki transakcji
    /// </summary>
    public void Enqueue(object payload, string? eventName = null)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0)
            throw new ObjectDisposedException(nameof(EventTransaction));

        if (Interlocked.CompareExchange(ref _isCommitted, 0, 0) != 0)
            throw new InvalidOperationException("Cannot add events after transaction is committed");

        _pendingEvents.Enqueue((payload, eventName));
    }

    /// <summary>
    /// Przetwarza wszystkie zdarzenia w transakcji sekwencyjnie
    /// </summary>
    public async Task CommitAsync()
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0)
            throw new ObjectDisposedException(nameof(EventTransaction));

        // Atomowo oznaczamy transakcjê jako zatwierdzon¹
        if (Interlocked.Exchange(ref _isCommitted, 1) != 0)
            throw new InvalidOperationException("Transaction already committed");

        // Przetwarzamy wszystkie zdarzenia w kolejce
        while (_pendingEvents.TryDequeue(out var eventData))
        {
            await _eventManager.PublishSequentialAsync(eventData.Payload, eventData.EventName);
        }
    }

    public void Dispose()
    {
        // Najpierw sprawdzamy, czy mo¿emy zacommitowaæ transakcjê
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 0)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isCommitted, 0, 0) == 0 && _pendingEvents.Count > 0)
                {
                    // Commit synchronicznie jeœli nie zatwierdzono
                    CommitAsync().GetAwaiter().GetResult();
                }
            }
            finally
            {
                // Wyrejestrujemy tê transakcjê z kontekstu, tylko jeœli to ta sama transakcja
                if (EventTransactionContext.GetCurrent() == this)
                {
                    EventTransactionContext.SetCurrent(null);
                }

                // Oznaczamy jako zwolniony
                Interlocked.Exchange(ref _isDisposed, 1);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Najpierw sprawdzamy, czy mo¿emy zacommitowaæ transakcjê
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 0)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isCommitted, 0, 0) == 0 && _pendingEvents.Count > 0)
                {
                    // Commit asynchronicznie
                    await CommitAsync();
                }
            }
            finally
            {
                // Wyrejestrujemy tê transakcjê z kontekstu, tylko jeœli to ta sama transakcja
                if (EventTransactionContext.GetCurrent() == this)
                {
                    EventTransactionContext.SetCurrent(null);
                }

                // Oznaczamy jako zwolniony
                Interlocked.Exchange(ref _isDisposed, 1);
            }
        }
    }
}
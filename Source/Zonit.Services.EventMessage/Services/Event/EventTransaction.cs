using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zonit.Services.EventMessage.Services.Event;

/// <summary>
/// Implementacja transakcji zdarzeñ zapewniaj¹ca sekwencyjne wykonywanie zdarzeñ w tle
/// </summary>
internal class EventTransaction : IEventTransaction
{
    private readonly EventManagerService _eventManager;
    private readonly ConcurrentQueue<(object Payload, string? EventName)> _pendingEvents = new();
    private readonly TaskCompletionSource _completionSource = new();

    // Flagi stanu
    private volatile bool _isDisposed;
    private volatile bool _isCommitted;
    private volatile bool _isProcessingStarted;

    public EventTransaction(EventManagerService eventManager)
    {
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));

        // Rejestracja tej transakcji jako aktywnej w bie¿¹cym kontekœcie
        EventTransactionContext.SetCurrent(this);
    }

    /// <summary>
    /// Zadanie, które zostanie ukoñczone, gdy wszystkie zdarzenia zostan¹ przetworzone
    /// </summary>
    public Task CompletionTask => _completionSource.Task;

    /// <summary>
    /// Dodaje zdarzenie do kolejki transakcji
    /// </summary>
    public void Enqueue(object payload, string? eventName = null)
    {
        ThrowIfDisposed();

        if (_isCommitted)
            throw new InvalidOperationException("Cannot add events after transaction is committed");

        _pendingEvents.Enqueue((payload, eventName));
    }

    /// <summary>
    /// Rozpoczyna przetwarzanie zdarzeñ w tle i zwraca natychmiast
    /// </summary>
    public Task CommitAsync()
    {
        ThrowIfDisposed();

        // Atomowo oznaczamy transakcjê jako zatwierdzon¹
        lock (this)
        {
            if (_isCommitted)
                throw new InvalidOperationException("Transaction already committed");

            _isCommitted = true;

            // Rozpoczynamy przetwarzanie tylko raz
            if (!_isProcessingStarted)
            {
                _isProcessingStarted = true;
                // Uruchamiamy przetwarzanie w tle
                _ = ProcessEventsInBackgroundAsync();
            }
        }

        // Zwracamy zadanie, które zostanie zakoñczone, gdy wszystkie zdarzenia zostan¹ przetworzone
        return CompletionTask;
    }

    /// <summary>
    /// Przetwarza wszystkie zdarzenia w transakcji sekwencyjnie w tle
    /// </summary>
    private async Task ProcessEventsInBackgroundAsync()
    {
        try
        {
            // Kopiujemy zdarzenia do tymczasowej listy, aby móc przetwarzaæ je sekwencyjnie
            var eventsToProcess = new List<(object Payload, string? EventName)>();
            while (_pendingEvents.TryDequeue(out var eventData))
            {
                eventsToProcess.Add(eventData);
            }

            // Przetwarzamy zdarzenia sekwencyjnie
            foreach (var (payload, eventName) in eventsToProcess)
            {
                await _eventManager.PublishSequentialAsync(payload, eventName);
            }

            // Oznaczamy zadanie jako ukoñczone pomyœlnie
            _completionSource.TrySetResult();
        }
        catch (Exception ex)
        {
            // Jeœli wyst¹pi³ b³¹d, oznaczamy zadanie jako zakoñczone b³êdem
            _completionSource.TrySetException(ex);
        }
    }

    // Prywatna metoda pomocnicza do sprawdzania czy obiekt jest zwolniony
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(EventTransaction));
    }

    public void Dispose()
    {
        // W nietypowych przypadkach mo¿emy mieæ wielokrotne wywo³ania Dispose()
        if (_isDisposed)
            return;

        // Opcjonalne zatwierdzenie transakcji, bez oczekiwania na zakoñczenie
        if (!_isCommitted && _pendingEvents.Count > 0)
        {
            try
            {
                // Rozpoczynamy zatwierdzenie bez blokowania
                _ = CommitAsync();
            }
            catch (ObjectDisposedException)
            {
                // Ignorujemy ten wyj¹tek w przypadku rzadkiego wyœcigu
            }
        }

        // Oznaczamy obiekt jako zwolniony
        _isDisposed = true;

        // Wyrejestrujemy tê transakcjê z kontekstu, tylko jeœli to ta sama transakcja
        if (EventTransactionContext.GetCurrent() == this)
        {
            EventTransactionContext.SetCurrent(null);
        }
    }

    public ValueTask DisposeAsync()
    {
        // U¿ywamy tej samej logiki co w Dispose()
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Czeka na zakoñczenie wszystkich operacji transakcji - mo¿na wywo³aæ rêcznie, jeœli jest potrzeba
    /// </summary>
    public async Task WaitForCompletionAsync()
    {
        if (!_isDisposed && !_isCommitted && _pendingEvents.Count > 0)
        {
            await CommitAsync();
        }

        await CompletionTask;
    }
}
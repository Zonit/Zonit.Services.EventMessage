using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zonit.Services.EventMessage.Services.Event;

/// <summary>
/// Implementacja transakcji zdarze� zapewniaj�ca sekwencyjne wykonywanie zdarze� w tle
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

        // Rejestracja tej transakcji jako aktywnej w bie��cym kontek�cie
        EventTransactionContext.SetCurrent(this);
    }

    /// <summary>
    /// Zadanie, kt�re zostanie uko�czone, gdy wszystkie zdarzenia zostan� przetworzone
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
    /// Rozpoczyna przetwarzanie zdarze� w tle i zwraca natychmiast
    /// </summary>
    public Task CommitAsync()
    {
        ThrowIfDisposed();

        // Atomowo oznaczamy transakcj� jako zatwierdzon�
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

        // Zwracamy zadanie, kt�re zostanie zako�czone, gdy wszystkie zdarzenia zostan� przetworzone
        return CompletionTask;
    }

    /// <summary>
    /// Przetwarza wszystkie zdarzenia w transakcji sekwencyjnie w tle
    /// </summary>
    private async Task ProcessEventsInBackgroundAsync()
    {
        try
        {
            // Kopiujemy zdarzenia do tymczasowej listy, aby m�c przetwarza� je sekwencyjnie
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

            // Oznaczamy zadanie jako uko�czone pomy�lnie
            _completionSource.TrySetResult();
        }
        catch (Exception ex)
        {
            // Je�li wyst�pi� b��d, oznaczamy zadanie jako zako�czone b��dem
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
        // W nietypowych przypadkach mo�emy mie� wielokrotne wywo�ania Dispose()
        if (_isDisposed)
            return;

        // Opcjonalne zatwierdzenie transakcji, bez oczekiwania na zako�czenie
        if (!_isCommitted && _pendingEvents.Count > 0)
        {
            try
            {
                // Rozpoczynamy zatwierdzenie bez blokowania
                _ = CommitAsync();
            }
            catch (ObjectDisposedException)
            {
                // Ignorujemy ten wyj�tek w przypadku rzadkiego wy�cigu
            }
        }

        // Oznaczamy obiekt jako zwolniony
        _isDisposed = true;

        // Wyrejestrujemy t� transakcj� z kontekstu, tylko je�li to ta sama transakcja
        if (EventTransactionContext.GetCurrent() == this)
        {
            EventTransactionContext.SetCurrent(null);
        }
    }

    public ValueTask DisposeAsync()
    {
        // U�ywamy tej samej logiki co w Dispose()
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Czeka na zako�czenie wszystkich operacji transakcji - mo�na wywo�a� r�cznie, je�li jest potrzeba
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
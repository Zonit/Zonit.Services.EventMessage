namespace Zonit.Services.EventMessage.Services;

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage.Abstractions.Managers;

/// <summary>
/// Implementacja usługi Event Bus z kontrolą współbieżności dla subskrypcji
/// </summary>
public class EventManagerService : IEventManager, IDisposable
{
    private readonly ILogger<EventManagerService> _logger;
    private readonly CancellationTokenSource _globalCts = new();
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
    private readonly int _defaultWorkerCount = 1;

    public EventManagerService(ILogger<EventManagerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Przechowuje subskrypcje i ich semafory w jednej strukturze dla lepszej spójności
    private class Subscription
    {
        public Subscription(Func<PayloadModel, Task> handler, int workerCount, TimeSpan timeout)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Semaphore = new SemaphoreSlim(workerCount, workerCount);
            Timeout = timeout;
        }

        public Func<PayloadModel, Task> Handler { get; }
        public SemaphoreSlim Semaphore { get; }
        public TimeSpan Timeout { get; }
    }

    // Słownik przechowujący listy subskrypcji dla nazw zdarzeń
    private readonly ConcurrentDictionary<string, List<Subscription>> _subscriptions = new();

    /// <summary>
    /// Subskrybuje zdarzenie o określonej nazwie
    /// </summary>
    /// <param name="eventName">Nazwa zdarzenia</param>
    /// <param name="handler">Funkcja obsługi zdarzenia</param>
    /// <param name="workers">Liczba równolegle obsługiwanych zdarzeń dla tej subskrypcji</param>
    /// <param name="timeout">Limit czasu na wykonanie operacji</param>
    public void Subscribe(string eventName, Func<PayloadModel, Task> handler, int? workers = null, TimeSpan? timeout = null)
    {
        if (string.IsNullOrEmpty(eventName))
            throw new ArgumentException("Event name cannot be null or empty", nameof(eventName));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        int workerCount = workers ?? _defaultWorkerCount;
        if (workerCount <= 0)
            throw new ArgumentException("Worker count must be greater than zero", nameof(workers));

        TimeSpan timeoutValue = timeout ?? _defaultTimeout;

        var subscription = new Subscription(handler, workerCount, timeoutValue);

        _subscriptions.AddOrUpdate(
            eventName,
            _ => new List<Subscription> { subscription },
            (_, existingList) =>
            {
                lock (existingList)
                {
                    existingList.Add(subscription);
                    return existingList;
                }
            });

        _logger.LogInformation("Subscribed to event '{EventName}' with {WorkerCount} workers and {Timeout}ms timeout",
            eventName, workerCount, timeoutValue.TotalMilliseconds);
    }

    /// <summary>
    /// Subskrybuje zdarzenie generyczne
    /// </summary>
    /// <typeparam name="TModel">Typ danych zdarzenia</typeparam>
    /// <param name="handler">Funkcja obsługi zdarzenia</param>
    /// <param name="workers">Liczba równolegle obsługiwanych zdarzeń</param>
    /// <param name="timeout">Limit czasu na wykonanie operacji</param>
    public void Subscribe<TModel>(Func<PayloadModel<TModel>, Task> handler, int? workers = null, TimeSpan? timeout = null)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        string eventName = typeof(TModel).FullName!;

        // Adapter do konwersji między PayloadModel<TModel> a PayloadModel
        Task AdapterHandler(PayloadModel payload)
        {
            if (payload.Data is TModel data)
            {
                var typedPayload = new PayloadModel<TModel>
                {
                    Data = data,
                    CancellationToken = payload.CancellationToken
                };
                return handler(typedPayload);
            }

            _logger.LogError("Invalid payload type for event '{EventName}'. Expected {ExpectedType}, got {ActualType}.",
                eventName, typeof(TModel).Name, payload.Data?.GetType().Name ?? "null");
            return Task.CompletedTask;
        }

        Subscribe(eventName, AdapterHandler, workers, timeout);
    }

    /// <summary>
    /// Publikuje zdarzenie
    /// </summary>
    /// <param name="payload">Dane zdarzenia</param>
    /// <param name="eventName">Opcjonalna nazwa zdarzenia</param>
    public void Publish(object payload, string? eventName = null)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        string actualEventName = eventName ?? payload.GetType().FullName!;

        if (_subscriptions.TryGetValue(actualEventName, out var subscriptions))
        {
            var payloadModel = new PayloadModel
            {
                Data = payload,
                CancellationToken = _globalCts.Token
            };

            foreach (var subscription in subscriptions)
            {
                // Uruchamiamy przetwarzanie zdarzenia, ale nie czekamy na jego zakończenie
                _ = ProcessEventAsync(actualEventName, subscription, payloadModel);
            }

            _logger.LogDebug("Published event '{EventName}' to {SubscribersCount} subscribers", actualEventName, subscriptions.Count);
        }
        else
        {
            _logger.LogDebug("No subscribers for event '{EventName}'", actualEventName);
        }
    }

    /// <summary>
    /// Asynchronicznie przetwarza zdarzenie z użyciem semafora do kontroli współbieżności
    /// </summary>
    private async Task ProcessEventAsync(string eventName, Subscription subscription, PayloadModel payload)
    {
        // Uzyskanie tokena anulowania z uwzględnieniem globalnego tokena
        using var timeoutCts = new CancellationTokenSource(subscription.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            payload.CancellationToken,
            timeoutCts.Token,
            _globalCts.Token);
        var token = linkedCts.Token;

        try
        {
            // Oczekiwanie na dostępność semafora
            await subscription.Semaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                await subscription.Handler(payload).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (timeoutCts.IsCancellationRequested)
                    _logger.LogWarning("Event processing timed out for '{EventName}'", eventName);
                else
                    _logger.LogDebug("Event processing cancelled for '{EventName}'", eventName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event '{EventName}'", eventName);
            }
            finally
            {
                subscription.Semaphore.Release();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (timeoutCts.IsCancellationRequested)
                _logger.LogWarning("Timeout waiting for semaphore for event '{EventName}'", eventName);
            else
                _logger.LogDebug("Waiting for semaphore cancelled for event '{EventName}'", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for semaphore for event '{EventName}'", eventName);
        }
    }

    /// <summary>
    /// Zwalnia wszystkie zasoby używane przez instancję EventManagerService
    /// </summary>
    public void Dispose()
    {
        try
        {
            // Anulujemy wszystkie trwające operacje
            _globalCts.Cancel();

            // Zwalniamy wszystkie semafory
            foreach (var subscriptionList in _subscriptions.Values)
            {
                foreach (var subscription in subscriptionList)
                {
                    subscription.Semaphore.Dispose();
                }
            }

            // Czyścimy słownik subskrypcji
            _subscriptions.Clear();

            // Zwalniamy źródło tokenów anulowania
            _globalCts.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during EventManagerService disposal");
        }
    }
}

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Zonit.Services.EventMessage.Services;

// Dodać wsparcie dla Entites, czyli np id użytkownika, id główne, id projektu, id organizacji, tak by gdzieś dodatkowo te dane już były zawarte
// Może dodać szablon, frame, ramkę jakąś w które jest właśnie informacja o organizacji itp
// Dodać cancel token w zadaniu, np tak by długo to nie trwało
// dodać timeout w zadaniu, domyślnie zdefiniować wartość oraz dynamicznie w Subcribe
// Czy zadanie ma się powtarzać, dodatkowa opcja coś na zasadzie crona. Wykona się zadanie co np 5 minut 
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class EventBusService(ILogger<EventBusService> logger) : IEventProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, List<Func<PayloadModel, Task>>> _eventSubscriptions = new();
    private readonly ConcurrentDictionary<string, List<SemaphoreSlim>> _eventSemaphores = new(); // Kolejki dla subskrypcji
    private readonly ILogger<EventBusService> _logger = logger;
    private readonly CancellationTokenSource _globalCts = new();

    public void Subscribe(string eventName, int workerCount, Func<PayloadModel, Task> handler)
    {
        // Tworzymy osobną semafor dla każdej subskrypcji
        var semaphore = new SemaphoreSlim(workerCount);

        // Dodajemy subskrypcję do listy subskrypcji dla danego eventu
        var subscriptions = _eventSubscriptions.GetOrAdd(eventName, _ => new List<Func<PayloadModel, Task>>());
        subscriptions.Add(handler);

        // Dodajemy semafor do osobnej listy dla każdej subskrypcji
        var semaphores = _eventSemaphores.GetOrAdd(eventName, _ => new List<SemaphoreSlim>());
        semaphores.Add(semaphore);

        _logger.LogInformation($"Subscribed to event '{eventName}' with {workerCount} workers.");
    }

    public void Publish(string eventName, object payload)
    {
        if (_eventSubscriptions.TryGetValue(eventName, out var handlers))
        {
            if (_eventSemaphores.TryGetValue(eventName, out var semaphores))
            {
                for (int i = 0; i < handlers.Count; i++)
                {
                    var handler = handlers[i];
                    var semaphore = semaphores[i];
                    // Przekazujemy każdy handler i semafor do osobnego przetwarzania
                    _ = ProcessHandler(eventName, handler, semaphore, payload);
                }
            }
        }
        else
        {
            _logger.LogWarning($"No subscribers for event '{eventName}'");
        }
    }

    private async Task ProcessHandler(string eventName, Func<PayloadModel, Task> handler, SemaphoreSlim semaphore, object payload)
    {
        await semaphore.WaitAsync();

        try
        {
            await handler(new PayloadModel { Data = payload, CancellationToken = CancellationToken.None });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing event '{eventName}' with handler.");
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Dispose()
    {
        _globalCts.Cancel();
        foreach (var semaphores in _eventSemaphores.Values)
        {
            foreach (var semaphore in semaphores)
            {
                semaphore.Dispose();
            }
        }
    }
}


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage.Base;

namespace Zonit.Services.EventMessage;

public interface IEventHandler : IHandler { }

/// <summary>
/// Klasa bazowa dla handlerów zdarzeń, automatyzująca proces rejestracji
/// </summary>
public abstract class EventBase<TModel> : HandlerBase<TModel>, IEventHandler
{
    /// <summary>
    /// Nazwa zdarzenia, do którego subskrybuje handler
    /// </summary>
    protected virtual string EventName => typeof(TModel).FullName ?? typeof(TModel).Name;

    /// <summary>
    /// Domyślny czas wykonania handlera zdarzeń (nadpisuje domyślną wartość z HandlerBase)
    /// </summary>
    protected override TimeSpan ExecutionTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Rejestruje handler w systemie zdarzeń
    /// </summary>
    public override void Subscribe(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (string.IsNullOrEmpty(EventName))
        {
            throw new InvalidOperationException($"Event name cannot be null or empty in {GetType().Name}");
        }

        var eventManager = serviceProvider.GetRequiredService<IEventManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<EventBase<TModel>>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        logger.LogInformation("Registering event handler for '{EventName}' with {WorkerCount} workers",
            EventName, WorkerCount);

        // Używamy generycznej wersji subskrypcji jeśli możliwe
        if (eventManager.SupportsGenericSubscription())
        {
            eventManager.Subscribe<TModel>(CreateHandler(scopeFactory, logger), WorkerCount, ExecutionTimeout);
        }
        else
        {
            // Dla starszej wersji API używamy nietypowanej wersji
            eventManager.Subscribe(
                EventName,
                async payload =>
                {
                    try
                    {
                        var handler = CreateHandler(scopeFactory, logger);
                        if (payload.Data is TModel typedData)
                        {
                            // Konwertujemy PayloadModel<object> na PayloadModel<TModel>
                            var typedPayload = new PayloadModel<TModel>
                            {
                                Data = typedData,
                                CancellationToken = payload.CancellationToken
                            };

                            await handler(typedPayload);
                        }
                        else
                        {
                            logger.LogError("Expected event data of type {ExpectedType}, but got {ActualType}",
                                typeof(TModel).Name, payload.Data?.GetType().Name ?? "null");
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        logger.LogError(ex, "Error processing event '{EventName}'", EventName);
                    }
                },
                WorkerCount,
                ExecutionTimeout);
        }
    }
}

/// <summary>
/// Klasa bazowa dla handlerów zdarzeń z nietypowanymi danymi
/// </summary>
public abstract class EventBase : EventBase<object>
{
    /// <summary>
    /// Nazwa zdarzenia, do którego subskrybuje handler
    /// </summary>
    protected abstract override string EventName { get; }

    /// <summary>
    /// Właściwa metoda obsługi zdarzenia, do implementacji przez pochodne klasy
    /// </summary>
    /// <param name="data">Dane zdarzenia</param>
    /// <param name="cancellationToken">Token anulowania</param>
    protected abstract override Task HandleAsync(object data, CancellationToken cancellationToken);
}

public static class EventManagerExtensions
{
    /// <summary>
    /// Sprawdza czy IEventManager obsługuje generyczną subskrypcję
    /// </summary>
    public static bool SupportsGenericSubscription(this IEventManager eventManager)
    {
        // Sprawdzamy czy implementacja IEventManager obsługuje generyczne metody
        var type = eventManager.GetType();
        return type.GetMethod("Subscribe", [typeof(Func<PayloadModel<object>, Task>), typeof(int), typeof(TimeSpan)]) != null;
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Zonit.Services.EventMessage;

/// <summary>
/// Interfejs dla obsługi zdarzeń
/// </summary>
public interface IEventHandler
{
    /// <summary>
    /// Rejestruje handler w systemie zdarzeń
    /// </summary>
    void Subscribe(IServiceProvider serviceProvider);
}

/// <summary>
/// Klasa bazowa dla handlerów zdarzeń, automatyzująca proces rejestracji
/// </summary>
public abstract class EventBase : IEventHandler
{
    /// <summary>
    /// Nazwa zdarzenia, do którego subskrybuje handler
    /// </summary>
    protected abstract string EventName { get; }

    /// <summary>
    /// Liczba równoległych operacji obsługiwanych przez ten handler
    /// </summary>
    protected virtual int EventWorker { get; } = 10;

    /// <summary>
    /// Maksymalny czas wykonania handlera w sekundach (domyślnie 30 sekund)
    /// </summary>
    protected virtual TimeSpan ExecutionTimeoutSeconds { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Właściwa metoda obsługi zdarzenia, do implementacji przez pochodne klasy
    /// </summary>
    /// <param name="data">Dane zdarzenia</param>
    /// <param name="cancellationToken">Token anulowania</param>
    /// <returns>Task reprezentujący operację asynchroniczną</returns>
    protected abstract Task HandleAsync(object data, CancellationToken cancellationToken);

    /// <summary>
    /// Rejestruje handler w systemie zdarzeń
    /// </summary>
    /// <param name="serviceProvider">Dostawca usług DI</param>
    /// <exception cref="ArgumentNullException">Jeśli serviceProvider jest null</exception>
    public virtual void Subscribe(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (string.IsNullOrEmpty(EventName))
        {
            throw new InvalidOperationException($"Event name cannot be null or empty in {GetType().Name}");
        }

        // Preferujemy IEventManager, ale używamy IEventProvider jeśli IEventManager nie jest dostępny
        var eventManager = serviceProvider.GetService<IEventManager>()
            ?? serviceProvider.GetRequiredService<IEventProvider>() as IEventManager;

        if (eventManager == null)
        {
            throw new InvalidOperationException("Neither IEventManager nor IEventProvider implementing IEventManager is available in the service provider");
        }

        var logger = serviceProvider.GetRequiredService<ILogger<EventBase>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        logger.LogInformation("Registering event handler for '{EventName}' with {WorkerCount} workers",
            EventName, EventWorker);

        // Używamy timeout z konfiguracji klasy
        var timeout = ExecutionTimeoutSeconds;

        eventManager.Subscribe(
            EventName,
            async payload =>
            {
                // Każdy handler działa w swoim własnym scope IoC
                using var scope = scopeFactory.CreateScope();

                try
                {
                    logger.LogDebug("Processing event '{EventName}'", EventName);

                    // Rozwiązujemy konkretną instancję handlera z DI
                    if (scope.ServiceProvider.GetRequiredService(GetType()) is not EventBase handler)
                    {
                        logger.LogError("Failed to resolve handler for event '{EventName}' of type {HandlerType}",
                            EventName, GetType().Name);
                        return;
                    }

                    // Wykonujemy handler z przekazanym tokenem anulowania
                    await handler.HandleAsync(payload.Data, payload.CancellationToken);

                    logger.LogDebug("Event '{EventName}' processed successfully", EventName);
                }
                catch (OperationCanceledException) when (payload.CancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("Event '{EventName}' processing was cancelled", EventName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing event '{EventName}'", EventName);
                }
            },
            EventWorker,
            timeout);
    }
}

/// <summary>
/// Generyczna wersja klasy bazowej dla handlerów zdarzeń z typowanymi danymi
/// </summary>
/// <typeparam name="TData">Typ danych zdarzenia</typeparam>
public abstract class EventBase<TData> : EventBase
{
    /// <summary>
    /// Domyślnie używa pełnej nazwy typu jako nazwy zdarzenia
    /// </summary>
    protected override string EventName => typeof(TData).FullName ?? typeof(TData).Name;

    /// <summary>
    /// Metoda obsługi zdarzenia z typowanymi danymi
    /// </summary>
    /// <param name="data">Dane zdarzenia</param>
    /// <param name="cancellationToken">Token anulowania</param>
    protected abstract Task HandleAsync(TData data, CancellationToken cancellationToken);

    /// <summary>
    /// Implementacja bazowej metody HandleAsync, konwertująca dane do odpowiedniego typu
    /// </summary>
    protected override async Task HandleAsync(object data, CancellationToken cancellationToken)
    {
        if (data is TData typedData)
        {
            await HandleAsync(typedData, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException(
                $"Expected event data of type {typeof(TData).Name}, but got {data?.GetType().Name ?? "null"}");
        }
    }

    /// <summary>
    /// Zastępuje metodę Subscribe, aby wykorzystać generyczną metodę Subscribe z IEventManager
    /// </summary>
    public override void Subscribe(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (string.IsNullOrEmpty(EventName))
        {
            throw new InvalidOperationException($"Event name cannot be null or empty in {GetType().Name}");
        }

        // Próbujemy pobrać IEventManager bezpośrednio
        var eventManager = serviceProvider.GetService<IEventManager>();

        // Jeśli dostępny jest IEventManager, używamy generycznej subskrypcji
        if (eventManager != null)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<EventBase>>();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var timeout = ExecutionTimeoutSeconds;

            logger.LogInformation("Registering generic event handler for '{EventName}' with {WorkerCount} workers",
                EventName, EventWorker);

            eventManager.Subscribe<TData>(
                async payload =>
                {
                    // Każdy handler działa w swoim własnym scope IoC
                    using var scope = scopeFactory.CreateScope();

                    try
                    {
                        logger.LogDebug("Processing event '{EventName}'", EventName);

                        // Rozwiązujemy konkretną instancję handlera z DI
                        if (scope.ServiceProvider.GetRequiredService(GetType()) is not EventBase<TData> handler)
                        {
                            logger.LogError("Failed to resolve handler for event '{EventName}' of type {HandlerType}",
                                EventName, GetType().Name);
                            return;
                        }

                        // Wykonujemy handler z przekazanym tokenem anulowania
                        await handler.HandleAsync(payload.Data, payload.CancellationToken);

                        logger.LogDebug("Event '{EventName}' processed successfully", EventName);
                    }
                    catch (OperationCanceledException) when (payload.CancellationToken.IsCancellationRequested)
                    {
                        logger.LogInformation("Event '{EventName}' processing was cancelled", EventName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing event '{EventName}'", EventName);
                    }
                },
                EventWorker,
                timeout);
        }
        else
        {
            // Używamy podstawowej metody, jeśli IEventManager nie jest dostępny
            base.Subscribe(serviceProvider);
        }
    }
}

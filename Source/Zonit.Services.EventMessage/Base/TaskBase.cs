using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Zonit.Services.EventMessage;

/// <summary>
/// Interfejs dla obsługi zadań
/// </summary>
public interface ITaskHandler
{
    /// <summary>
    /// Rejestruje handler w systemie zadań
    /// </summary>
    void Subscribe(IServiceProvider serviceProvider);
}

/// <summary>
/// Generyczna klasa bazowa dla handlerów zadań z typowanymi danymi
/// </summary>
/// <typeparam name="TModel">Typ danych zadania</typeparam>
public abstract class TaskBase<TModel> : ITaskHandler
{
    /// <summary>
    /// Liczba równoległych operacji obsługiwanych przez ten handler
    /// </summary>
    protected virtual int TaskWorkers { get; } = 10;

    /// <summary>
    /// Maksymalny czas wykonania handlera
    /// </summary>
    protected virtual TimeSpan ExecutionTimeout { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Metoda obsługi zadania z typowanymi danymi
    /// </summary>
    /// <param name="data">Dane zadania</param>
    /// <param name="cancellationToken">Token anulowania</param>
    protected abstract Task HandleAsync(TModel data, CancellationToken cancellationToken);

    /// <summary>
    /// Rejestruje handler w systemie zadań
    /// </summary>
    /// <param name="serviceProvider">Dostawca usług DI</param>
    /// <exception cref="ArgumentNullException">Jeśli serviceProvider jest null</exception>
    public virtual void Subscribe(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var taskManager = serviceProvider.GetRequiredService<ITaskManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<TaskBase<TModel>>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        logger.LogInformation("Registering task handler for '{ModelType}' with {WorkerCount} workers",
            typeof(TModel).Name, TaskWorkers);

        // Rejestrujemy handler w TaskManager
        taskManager.Subscribe<TModel>(async (payload) =>
        {
            // Każdy handler działa w swoim własnym scope IoC
            using var scope = scopeFactory.CreateScope();

            // Tworzymy token z timeout dla wykonania handlera
            using var timeoutCts = new CancellationTokenSource(ExecutionTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token,
                payload.CancellationToken);

            var combinedToken = linkedCts.Token;

            try
            {
                logger.LogDebug("Processing task for model '{ModelType}'", typeof(TModel).Name);

                // Rozwiązujemy konkretną instancję handlera z DI
                if (scope.ServiceProvider.GetRequiredService(GetType()) is not TaskBase<TModel> handler)
                {
                    logger.LogError("Failed to resolve handler for model '{ModelType}' of type {HandlerType}",
                        typeof(TModel).Name, GetType().Name);
                    return;
                }

                // Wykonujemy handler z obsługą timeout
                await handler.HandleAsync(payload.Data, combinedToken);

                logger.LogDebug("Task for model '{ModelType}' processed successfully", typeof(TModel).Name);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogWarning("Task for model '{ModelType}' processing timed out after {Timeout}",
                    typeof(TModel).Name, ExecutionTimeout);
                throw; // Rzucamy wyjątek, aby TaskManager mógł oznaczyć zadanie jako nieudane
            }
            catch (OperationCanceledException) when (payload.CancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Task for model '{ModelType}' processing was cancelled", typeof(TModel).Name);
                throw; // Rzucamy wyjątek, aby TaskManager mógł oznaczyć zadanie jako anulowane
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing task for model '{ModelType}'", typeof(TModel).Name);
                throw; // Rzucamy wyjątek dalej, aby TaskManager mógł oznaczyć zadanie jako nieudane
            }
        }, TaskWorkers, ExecutionTimeout);
    }
}
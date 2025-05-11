using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Zonit.Services.EventMessage.Base;

/// <summary>
/// Abstrakcyjna klasa bazowa dla handlerów (zdarzeń i zadań)
/// </summary>
/// <typeparam name="T">Typ ładunku (payload) obsługiwany przez handler</typeparam>
public abstract class HandlerBase<T> : IHandler
{
    /// <summary>
    /// Liczba równoległych operacji obsługiwanych przez ten handler
    /// </summary>
    protected virtual int WorkerCount { get; } = 10;

    /// <summary>
    /// Maksymalny czas wykonania handlera
    /// </summary>
    protected virtual TimeSpan ExecutionTimeout { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Metoda obsługi z typowanymi danymi
    /// </summary>
    /// <param name="data">Dane do obsługi</param>
    /// <param name="cancellationToken">Token anulowania</param>
    protected abstract Task HandleAsync(T data, CancellationToken cancellationToken);

    /// <summary>
    /// Tworzy funkcję handlera wspólną dla wszystkich typów handlerów
    /// </summary>
    protected Func<PayloadModel<T>, Task> CreateHandler(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        return async (payload) =>
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
                logger.LogDebug("Processing handler for model '{ModelType}'", typeof(T).Name);

                // Rozwiązujemy konkretną instancję handlera z DI
                if (scope.ServiceProvider.GetRequiredService(GetType()) is not HandlerBase<T> handler)
                {
                    logger.LogError("Failed to resolve handler for model '{ModelType}' of type {HandlerType}",
                        typeof(T).Name, GetType().Name);
                    return;
                }

                // Wykonujemy handler z obsługą timeout
                await handler.HandleAsync(payload.Data, combinedToken);

                logger.LogDebug("Handler for model '{ModelType}' processed successfully", typeof(T).Name);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogWarning("Handler for model '{ModelType}' processing timed out after {Timeout}",
                    typeof(T).Name, ExecutionTimeout);
                throw; // Rzucamy wyjątek, aby manager mógł oznaczyć operację jako nieudaną
            }
            catch (OperationCanceledException) when (payload.CancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Handler for model '{ModelType}' processing was cancelled", typeof(T).Name);
                throw; // Rzucamy wyjątek, aby manager mógł oznaczyć operację jako anulowaną
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing handler for model '{ModelType}'", typeof(T).Name);
                throw; // Rzucamy wyjątek dalej, aby manager mógł oznaczyć operację jako nieudaną
            }
        };
    }

    /// <summary>
    /// Rejestruje handler w systemie
    /// </summary>
    public abstract void Subscribe(IServiceProvider serviceProvider);
}
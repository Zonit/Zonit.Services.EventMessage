using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage.Base;

namespace Zonit.Services.EventMessage;

public interface ITaskHandler : IHandler { }

/// <summary>
/// Klasa bazowa dla handlerów zadań
/// </summary>
/// <typeparam name="TModel">Typ danych zadania</typeparam>
public abstract class TaskBase<TModel> : HandlerBase<TModel>, ITaskHandler
{
    /// <summary>
    /// Rejestruje handler w systemie zadań
    /// </summary>
    public override void Subscribe(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var taskManager = serviceProvider.GetRequiredService<ITaskManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<TaskBase<TModel>>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        logger.LogInformation("Registering task handler for '{ModelType}' with {WorkerCount} workers",
            typeof(TModel).Name, WorkerCount);

        // Rejestrujemy handler w TaskManager
        taskManager.Subscribe<TModel>(CreateHandler(scopeFactory, logger), WorkerCount, ExecutionTimeout);
    }
}